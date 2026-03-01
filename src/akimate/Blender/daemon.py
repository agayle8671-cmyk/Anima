"""
akimate Blender Daemon — TCP Socket Server
==========================================
This script runs inside Blender's Python environment (via --python flag).
It opens an async TCP socket listener on localhost and waits for JSON commands
from the C# WinUI 3 application.

GPL INSULATION: Communication is strictly via TCP sockets. Zero shared memory
or linking between C# and Python/Blender code. The C# application remains an
"aggregate work" — not a derivative of Blender's GPL code.
"""

import bpy
import json
import socket
import struct
import threading
import traceback
import os
import sys
import uuid
from datetime import datetime


# ── Configuration ──────────────────────────────────────────────────────────
HOST = "127.0.0.1"
PORT = int(os.environ.get("AKIMATE_PORT", "9700"))
RENDER_DIR = os.environ.get("AKIMATE_RENDER_DIR", os.path.join(os.path.expanduser("~"), ".akimate", "renders"))
os.makedirs(RENDER_DIR, exist_ok=True)


# ── Protocol: Length-prefixed JSON messages ────────────────────────────────
# Each message: [4 bytes: uint32 length][N bytes: UTF-8 JSON]

def recv_message(sock):
    """Receive a length-prefixed JSON message from the socket."""
    raw_len = _recv_exact(sock, 4)
    if not raw_len:
        return None
    msg_len = struct.unpack(">I", raw_len)[0]
    if msg_len > 10 * 1024 * 1024:  # 10MB safety limit
        raise ValueError(f"Message too large: {msg_len} bytes")
    raw_msg = _recv_exact(sock, msg_len)
    if not raw_msg:
        return None
    return json.loads(raw_msg.decode("utf-8"))


def send_message(sock, data):
    """Send a length-prefixed JSON message over the socket."""
    payload = json.dumps(data).encode("utf-8")
    sock.sendall(struct.pack(">I", len(payload)) + payload)


def _recv_exact(sock, n):
    """Receive exactly n bytes from a socket."""
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            return None
        buf += chunk
    return buf


# ── Command Handlers ──────────────────────────────────────────────────────

def handle_ping(params):
    """Health check."""
    return {
        "blender_version": bpy.app.version_string,
        "timestamp": datetime.now().isoformat(),
        "scene": bpy.context.scene.name,
        "frame": bpy.context.scene.frame_current
    }


def handle_scene_info(params):
    """Return current scene information."""
    scene = bpy.context.scene
    objects = []
    for obj in scene.objects:
        objects.append({
            "name": obj.name,
            "type": obj.type,
            "location": list(obj.location),
            "rotation": list(obj.rotation_euler),
            "scale": list(obj.scale),
            "visible": obj.visible_get()
        })
    return {
        "scene_name": scene.name,
        "frame_start": scene.frame_start,
        "frame_end": scene.frame_end,
        "frame_current": scene.frame_current,
        "fps": scene.render.fps,
        "resolution_x": scene.render.resolution_x,
        "resolution_y": scene.render.resolution_y,
        "objects": objects
    }


def handle_create_object(params):
    """Create a primitive object in the scene."""
    obj_type = params.get("type", "cube").lower()
    location = tuple(params.get("location", [0, 0, 0]))
    name = params.get("name", "")

    # Track existing objects to find the new one
    existing = set(bpy.data.objects.keys())

    if obj_type == "cube":
        bpy.ops.mesh.primitive_cube_add(location=location)
    elif obj_type == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(location=location)
    elif obj_type == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(location=location)
    elif obj_type == "plane":
        bpy.ops.mesh.primitive_plane_add(location=location)
    elif obj_type == "camera":
        bpy.ops.object.camera_add(location=location)
    elif obj_type == "light":
        bpy.ops.object.light_add(type="POINT", location=location)
    else:
        return {"error": f"Unknown object type: {obj_type}"}

    # Find the newly created object by diffing
    new_objs = set(bpy.data.objects.keys()) - existing
    if not new_objs:
        return {"error": "Object was not created"}
    
    new_name = new_objs.pop()
    obj = bpy.data.objects[new_name]
    if name:
        obj.name = name

    return {
        "name": obj.name,
        "type": obj.type,
        "location": list(obj.location)
    }


def handle_delete_object(params):
    """Delete an object by name."""
    name = params.get("name", "")
    obj = bpy.data.objects.get(name)
    if obj is None:
        return {"error": f"Object not found: {name}"}
    bpy.data.objects.remove(obj, do_unlink=True)
    return {"deleted": name}


def handle_set_transform(params):
    """Set an object's location, rotation, and/or scale."""
    name = params.get("name", "")
    obj = bpy.data.objects.get(name)
    if obj is None:
        return {"error": f"Object not found: {name}"}

    if "location" in params:
        obj.location = tuple(params["location"])
    if "rotation" in params:
        obj.rotation_euler = tuple(params["rotation"])
    if "scale" in params:
        obj.scale = tuple(params["scale"])

    return {
        "name": obj.name,
        "location": list(obj.location),
        "rotation": list(obj.rotation_euler),
        "scale": list(obj.scale)
    }


def handle_set_keyframe(params):
    """Insert a keyframe for an object's property at the given frame."""
    name = params.get("name", "")
    frame = params.get("frame", bpy.context.scene.frame_current)
    data_path = params.get("data_path", "location")
    interpolation = params.get("interpolation", "CONSTANT")  # CONSTANT = stepped

    obj = bpy.data.objects.get(name)
    if obj is None:
        return {"error": f"Object not found: {name}"}

    bpy.context.scene.frame_set(frame)
    obj.keyframe_insert(data_path=data_path, frame=frame)

    # Set interpolation type on the fcurves (Blender 5.0 compatible)
    try:
        if obj.animation_data and obj.animation_data.action:
            action = obj.animation_data.action
            # Try Blender 5.0 API first (action.layers -> strips -> channels)
            if hasattr(action, 'layers'):
                for layer in action.layers:
                    for strip in layer.strips:
                        if hasattr(strip, 'channelbags'):
                            for channelbag in strip.channelbags:
                                for fcurve in channelbag.fcurves:
                                    if fcurve.data_path == data_path:
                                        for kp in fcurve.keyframe_points:
                                            if abs(kp.co[0] - frame) < 0.5:
                                                kp.interpolation = interpolation
            # Fallback: legacy Blender API (< 5.0)
            elif hasattr(action, 'fcurves'):
                for fcurve in action.fcurves:
                    if fcurve.data_path == data_path:
                        for kp in fcurve.keyframe_points:
                            if abs(kp.co[0] - frame) < 0.5:
                                kp.interpolation = interpolation
    except Exception as e:
        # Keyframe was inserted but interpolation couldn't be set
        print(f"[akimate] Warning: keyframe set but interpolation failed: {e}", flush=True)

    return {
        "name": obj.name,
        "frame": frame,
        "data_path": data_path,
        "interpolation": interpolation
    }


def handle_set_frame(params):
    """Set the current frame."""
    frame = params.get("frame", 1)
    bpy.context.scene.frame_set(frame)
    return {"frame": bpy.context.scene.frame_current}


def handle_set_fps(params):
    """Set the scene frame rate."""
    fps = params.get("fps", 24)
    bpy.context.scene.render.fps = fps
    return {"fps": bpy.context.scene.render.fps}


def handle_set_frame_range(params):
    """Set the start and end frame."""
    bpy.context.scene.frame_start = params.get("start", 1)
    bpy.context.scene.frame_end = params.get("end", 250)
    return {
        "frame_start": bpy.context.scene.frame_start,
        "frame_end": bpy.context.scene.frame_end
    }


def handle_render_frame(params):
    """Render the current frame to an image file and return the path."""
    frame = params.get("frame", bpy.context.scene.frame_current)
    resolution_x = params.get("resolution_x", 1920)
    resolution_y = params.get("resolution_y", 1080)
    samples = params.get("samples", 64)
    engine = params.get("engine", "BLENDER_EEVEE_NEXT")  # or CYCLES

    scene = bpy.context.scene
    scene.frame_set(frame)
    scene.render.resolution_x = resolution_x
    scene.render.resolution_y = resolution_y
    scene.render.engine = engine
    scene.render.image_settings.file_format = "PNG"

    if engine == "CYCLES":
        scene.cycles.samples = samples

    filename = f"frame_{frame:04d}_{uuid.uuid4().hex[:8]}.png"
    filepath = os.path.join(RENDER_DIR, filename)
    scene.render.filepath = filepath

    bpy.ops.render.render(write_still=True)

    return {
        "filepath": filepath,
        "frame": frame,
        "resolution": [resolution_x, resolution_y],
        "engine": engine
    }


def handle_render_settings(params):
    """Configure render settings without rendering."""
    scene = bpy.context.scene
    if "resolution_x" in params:
        scene.render.resolution_x = params["resolution_x"]
    if "resolution_y" in params:
        scene.render.resolution_y = params["resolution_y"]
    if "engine" in params:
        scene.render.engine = params["engine"]
    if "fps" in params:
        scene.render.fps = params["fps"]
    if "film_transparent" in params:
        scene.render.film_transparent = params["film_transparent"]

    return {
        "resolution_x": scene.render.resolution_x,
        "resolution_y": scene.render.resolution_y,
        "engine": scene.render.engine,
        "fps": scene.render.fps
    }


def handle_import_file(params):
    """Import a 3D file (FBX, OBJ, glTF, etc.)."""
    filepath = params.get("filepath", "")
    if not os.path.exists(filepath):
        return {"error": f"File not found: {filepath}"}

    ext = os.path.splitext(filepath)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=filepath)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=filepath)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=filepath)
    elif ext == ".blend":
        with bpy.data.libraries.load(filepath, link=False) as (data_from, data_to):
            data_to.objects = data_from.objects
        for obj in data_to.objects:
            if obj is not None:
                bpy.context.collection.objects.link(obj)
    else:
        return {"error": f"Unsupported format: {ext}"}

    return {"imported": filepath, "objects": [o.name for o in bpy.context.selected_objects]}


def handle_save_blend(params):
    """Save the current scene as a .blend file."""
    filepath = params.get("filepath", "")
    if not filepath:
        return {"error": "No filepath specified"}
    bpy.ops.wm.save_as_mainfile(filepath=filepath)
    return {"saved": filepath}


def handle_new_scene(params):
    """Clear the scene and start fresh."""
    bpy.ops.wm.read_homefile(use_empty=True)
    return {"scene": bpy.context.scene.name}


def handle_execute_python(params):
    """Execute arbitrary Python code in Blender's context.
    WARNING: This is powerful — use only for agent-generated Blender scripts."""
    code = params.get("code", "")
    if not code:
        return {"error": "No code provided"}

    result_holder = {}
    exec_globals = {"bpy": bpy, "result": result_holder}
    exec(code, exec_globals)

    return {"executed": True, "result": result_holder.get("value", None)}


# ── Command Router ────────────────────────────────────────────────────────

COMMAND_MAP = {
    "ping": handle_ping,
    "scene_info": handle_scene_info,
    "create_object": handle_create_object,
    "delete_object": handle_delete_object,
    "set_transform": handle_set_transform,
    "set_keyframe": handle_set_keyframe,
    "set_frame": handle_set_frame,
    "set_fps": handle_set_fps,
    "set_frame_range": handle_set_frame_range,
    "render_frame": handle_render_frame,
    "render_settings": handle_render_settings,
    "import_file": handle_import_file,
    "save_blend": handle_save_blend,
    "new_scene": handle_new_scene,
    "execute_python": handle_execute_python,
}


def process_command(message):
    """Route a command message to the appropriate handler."""
    command = message.get("command", "")
    params = message.get("params", {})
    request_id = message.get("request_id", "")

    handler = COMMAND_MAP.get(command)
    if handler is None:
        return {
            "request_id": request_id,
            "status": "error",
            "error": f"Unknown command: {command}",
            "available_commands": list(COMMAND_MAP.keys())
        }

    try:
        result = handler(params)
        return {
            "request_id": request_id,
            "status": "ok",
            "command": command,
            "result": result
        }
    except Exception as e:
        return {
            "request_id": request_id,
            "status": "error",
            "command": command,
            "error": str(e),
            "traceback": traceback.format_exc()
        }


# ── Client Handler ────────────────────────────────────────────────────────

def handle_client(client_sock, addr):
    """Handle a single client connection."""
    print(f"[akimate] Client connected: {addr}", flush=True)
    try:
        while True:
            message = recv_message(client_sock)
            if message is None:
                break

            # Process command on Blender's main thread via timer
            response = process_command(message)
            send_message(client_sock, response)

    except (ConnectionResetError, BrokenPipeError):
        pass
    except Exception as e:
        print(f"[akimate] Client error: {e}", flush=True)
        traceback.print_exc()
    finally:
        client_sock.close()
        print(f"[akimate] Client disconnected: {addr}", flush=True)


# ── Server ────────────────────────────────────────────────────────────────

def start_server():
    """Start the TCP socket server in a background thread."""
    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_sock.bind((HOST, PORT))
    server_sock.listen(1)
    print(f"[akimate] Blender daemon listening on {HOST}:{PORT}", flush=True)
    print(f"[akimate] Blender version: {bpy.app.version_string}", flush=True)
    print(f"[akimate] Render output: {RENDER_DIR}", flush=True)

    # Signal that we're ready (write a marker file)
    ready_path = os.environ.get("AKIMATE_READY_FILE", "")
    if ready_path:
        with open(ready_path, "w") as f:
            f.write(f"{PORT}\n")
        print(f"[akimate] Ready signal written to: {ready_path}", flush=True)

    while True:
        try:
            client_sock, addr = server_sock.accept()
            client_thread = threading.Thread(target=handle_client, args=(client_sock, addr), daemon=True)
            client_thread.start()
        except Exception as e:
            print(f"[akimate] Accept error: {e}", flush=True)
            break

    server_sock.close()


# ── Entry Point ───────────────────────────────────────────────────────

if __name__ == "__main__" or True:  # Always run when loaded by Blender
    print("[akimate] Daemon initialized — starting server on main thread.", flush=True)
    # Run server on the MAIN thread to keep Blender alive in --background mode.
    # Client connections are handled in daemon threads (see handle_client).
    start_server()
