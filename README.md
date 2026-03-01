# akimate — AI Anime Production Studio

An AI-assisted anime production studio packaged as a Windows desktop application. Users input an idea and are guided through every phase of the anime pipeline — from concept to broadcast-ready episode.

## Tech Stack

- **Frontend:** WinUI 3 / Windows App SDK (.NET 8)
- **AI Orchestration:** Microsoft Semantic Kernel
- **3D Engine:** Blender 4.3 (headless, via IPC)
- **Local AI:** ONNX Runtime + DirectML
- **Cloud AI:** Runway Gen-3 Alpha, OpenAI Sora 2

## Building

```
dotnet restore src/akimate/akimate.csproj
dotnet build src/akimate/akimate.csproj
dotnet run --project src/akimate/akimate.csproj
```

## License

Proprietary. All rights reserved.
