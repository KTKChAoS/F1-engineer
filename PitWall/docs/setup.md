# PitWall — Setup Guide

## Prerequisites

- A Windows PC on the same network as your PS5
- .NET 10.0 Runtime (or SDK for development)
- Node.js 22.12+ (for web client development)

## PS5 Configuration (F1 25)

1. Open **F1 25** → **Settings** → **Telemetry Settings**
2. Set the following:
   - **UDP Telemetry:** On
   - **UDP Broadcast Mode:** Off
   - **UDP IP Address:** Your PC's IPv4 address (e.g., `192.168.1.100`)
   - **UDP Port:** `20777`
   - **UDP Send Rate:** `20 Hz` (up to 60 Hz supported)
   - **UDP Format:** `2025`

> ⚠️ If the packet header reports a different format, the app will warn you to set UDP Format to **2025**.

## Find Your PC's IP Address

Open PowerShell and run:
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -match '^(192\.168|10\.|172\.(1[6-9]|2[0-9]|3[01]))' } | Select-Object IPAddress, InterfaceAlias
```

## Reserve a Fixed IP

In your router's admin panel, reserve the PC's current IP so it doesn't change via DHCP.

## Windows Firewall

The app offers a button to add firewall rules automatically (requires elevation).

Alternatively, run these commands in an elevated PowerShell:
```powershell
netsh advfirewall firewall add rule name="PitWall UDP In" dir=in action=allow protocol=UDP localport=20777
netsh advfirewall firewall add rule name="PitWall TCP In" dir=in action=allow protocol=TCP localport=5080
```

## Running the Host

```bash
cd src/PitWall.Host
dotnet run
```

## Running the Web Client (Development)

```bash
cd src/PitWall.Web
npm install
npm run dev
```

## Running the Simulator

```bash
cd tools/PitWall.Sim
dotnet run
```
