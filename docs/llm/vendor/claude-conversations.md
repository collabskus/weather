00
22

Container action is still failing 
API container lifecycle
failed now in 36s
1s
1s
32s
Run bash ci/containers.sh api-lifecycle
==> podman version
Client:       Podman Engine
Version:      4.9.3
API Version:  4.9.3
Go Version:   go1.22.2
Built:        Thu Jan  1 00:00:00 1970
OS/Arch:      linux/amd64
==> building weather-api:ci
[1/2] STEP 1/5: FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
Trying to pull mcr.microsoft.com/dotnet/sdk:10.0...
Getting image source signatures
Copying blob sha256:e46b7fd4cb80bf93f920809a4b319d5f392f216831b99a09d67d8a3deed0e5a3
Copying blob sha256:01a6e70bac151d9170a172c07eeaed4f182bfbad2274cad97ce78a24e4b26be9
Copying blob sha256:995fee283d28c6382dc507ba2055e03cc3418053ecdff5f1d7bb18839d3e0307
Copying blob sha256:e811eee7b15b830390375db6937b0fbe0086babc8d60cdf07438be2cdde633a8
Copying blob sha256:a0489bf764ead14835c968e03bb9c38a44a8a72fb333f5264f5849d97c372321
Copying blob sha256:cb259a83ac3dd9fea0b394df41df2b298adf0df938fef5999475af18a751c257
Copying blob sha256:0e9914cbc886d9912d54d5b4a987125ed7c117be62c9331f679a763475b233d9
Copying blob sha256:5f8bd1ac25dc642e683783959d22e7f4b3adb4e46a4a94a34bf70f812b65b05e
Copying blob sha256:afe634a74685a7257c983a758b6cadbca523bd027b815574bc59d62e166ae8fb
Copying blob sha256:ff73fa535b6d4571805f6ba72ff99953588b8e2eb832ea17d8721e595512477d
Copying config sha256:2588552431551649d5503c5553b6a6300586f25310697ad2bdc6cb702f18bbb9
Writing manifest to image destination
[1/2] STEP 2/5: WORKDIR /src
--> f010181e104d
[1/2] STEP 3/5: COPY . .
--> 56ff2e0545c8
[1/2] STEP 4/5: RUN dotnet restore src/Weather.Api/Weather.Api.csproj
  Determining projects to restore...
  Restored /src/src/Weather.Core/Weather.Core.csproj (in 116 ms).
  Restored /src/src/Weather.ServiceDefaults/Weather.ServiceDefaults.csproj (in 1.81 sec).
  Restored /src/src/Weather.Api/Weather.Api.csproj (in 2.23 sec).
  Restored /src/src/Weather.Infrastructure/Weather.Infrastructure.csproj (in 2.24 sec).
time="2026-06-22T22:41:16Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/28d92f40206910d585c2aa8ce27359b0cb2d58b8d26c0770855d1a692e9f5218/merged/tmp/MSBuild27\" since it is a socket"
time="2026-06-22T22:41:16Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/28d92f40206910d585c2aa8ce27359b0cb2d58b8d26c0770855d1a692e9f5218/merged/tmp/MSBuild28\" since it is a socket"
time="2026-06-22T22:41:16Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/28d92f40206910d585c2aa8ce27359b0cb2d58b8d26c0770855d1a692e9f5218/merged/tmp/dotnet-diagnostic-27-70125-socket\" since it is a socket"
time="2026-06-22T22:41:16Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/28d92f40206910d585c2aa8ce27359b0cb2d58b8d26c0770855d1a692e9f5218/merged/tmp/dotnet-diagnostic-28-70125-socket\" since it is a socket"
--> e8d650b022a9
[1/2] STEP 5/5: RUN dotnet publish src/Weather.Api/Weather.Api.csproj     -c Release     --no-restore     -o /app/publish     -p:UseAppHost=false
  Weather.Core -> /src/src/Weather.Core/bin/Release/net10.0/Weather.Core.dll
  Weather.ServiceDefaults -> /src/src/Weather.ServiceDefaults/bin/Release/net10.0/Weather.ServiceDefaults.dll
  Weather.Infrastructure -> /src/src/Weather.Infrastructure/bin/Release/net10.0/Weather.Infrastructure.dll
  Weather.Api -> /src/src/Weather.Api/bin/Release/net10.0/Weather.Api.dll
  Weather.Api -> /app/publish/
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/FAaUMCy0F1oDHv7sgIzVMq3ghncCBo7YPCn3eGxpOxk\" since it is a socket"
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/MSBuild30\" since it is a socket"
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/MSBuild31\" since it is a socket"
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/dotnet-diagnostic-30-70790-socket\" since it is a socket"
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/dotnet-diagnostic-31-70790-socket\" since it is a socket"
time="2026-06-22T22:41:26Z" level=warning msg="archive: skipping \"/var/lib/containers/storage/overlay/29b523bee87bf1e9464b012dd6650adaa9374f1cc5a65032b07e956dac5f6488/merged/tmp/dotnet-diagnostic-72-70870-socket\" since it is a socket"
--> 8bbfda6e4cdb
[2/2] STEP 1/9: FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
Trying to pull mcr.microsoft.com/dotnet/aspnet:10.0...
Getting image source signatures
Copying blob sha256:e46b7fd4cb80bf93f920809a4b319d5f392f216831b99a09d67d8a3deed0e5a3
Copying blob sha256:cb259a83ac3dd9fea0b394df41df2b298adf0df938fef5999475af18a751c257
Copying blob sha256:e811eee7b15b830390375db6937b0fbe0086babc8d60cdf07438be2cdde633a8
Copying blob sha256:01a6e70bac151d9170a172c07eeaed4f182bfbad2274cad97ce78a24e4b26be9
Copying blob sha256:a0489bf764ead14835c968e03bb9c38a44a8a72fb333f5264f5849d97c372321
Copying blob sha256:995fee283d28c6382dc507ba2055e03cc3418053ecdff5f1d7bb18839d3e0307
Copying config sha256:4bb9bff6c9b0acb6875a8d5ea4b4ee22369834f4109a2ab0849d2d7fe02d1ea3
Writing manifest to image destination
[2/2] STEP 2/9: WORKDIR /app
--> 12daa3359265
[2/2] STEP 3/9: ENV ASPNETCORE_URLS=http://+:8080     ASPNETCORE_HTTP_PORTS=8080     DOTNET_EnableDiagnostics=0
--> 9fa5660f9e19
[2/2] STEP 4/9: RUN mkdir -p /data && chown 1654:1654 /data
--> 0bdcdc69add5
[2/2] STEP 5/9: VOLUME ["/data"]
--> 76b777c1ba21
[2/2] STEP 6/9: COPY --from=build --chown=1654:1654 /app/publish .
--> 400927bcc49f
[2/2] STEP 7/9: USER 1654
--> bbc2348bb3d6
[2/2] STEP 8/9: EXPOSE 8080
--> a5d051e33d6c
[2/2] STEP 9/9: ENTRYPOINT ["dotnet", "Weather.Api.dll"]
[2/2] COMMIT weather-api:ci
--> 52585f5b77f9
Successfully tagged localhost/weather-api:ci
52585f5b77f9a3f602e49e3f779946908fa8ecb11d841bedf56798ccaf681baf
==> starting weather-api-ci (publishing :8080)
5784de17297ec5a0f7a445e540e04f47a9a87dfebd7623a84d0192b8ccae8339
==> waiting for http://localhost:8080/alive
  attempt  1/30: /alive -> 000
  attempt  2/30: /alive -> 200
==> API is live
==> stopping and removing weather-api-ci
weather-api-ci
weather-api-ci
==> container created and destroyed cleanly
ci/containers.sh: line 127: name: unbound variable
Error: Process completed with exit code 1.

