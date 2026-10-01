#!/bin/zsh
cd "$(dirname "$0")"
(
  for attempt in {1..60}; do
    if curl -sf http://127.0.0.1:5187/ >/dev/null; then
      open http://127.0.0.1:5187
      exit 0
    fi
    sleep 1
  done
) &
dotnet run --project HeadArena.csproj
