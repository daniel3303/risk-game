FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
COPY global.json Directory.*.props ./
COPY src/Risk.Sim/ src/Risk.Sim/
COPY tools/Risk.Training/ tools/Risk.Training/
COPY content/classic.json content/classic.json
RUN dotnet publish tools/Risk.Training -c Release -o /bridge

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
FROM python:3.12-slim-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends libicu72 && rm -rf /var/lib/apt/lists/*
COPY --from=runtime /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 MPLCONFIGDIR=/tmp/matplotlib
WORKDIR /training
COPY training/requirements.txt ./
RUN pip install --no-cache-dir torch==2.14.1 --index-url https://download.pytorch.org/whl/cpu \
    && pip install --no-cache-dir -r requirements.txt
COPY --from=build /bridge /bridge
COPY training/ ./
ENV RISK_BRIDGE=/bridge/Risk.Training.dll
ENTRYPOINT ["python", "-m", "risk_training"]
