# PitWall.Web

React, TypeScript and Vite client scaffold. Zustand is set up for client state, Tailwind CSS for styling, and uPlot is included for future telemetry charts. Host and relay WebSocket connections and dashboard pages will be implemented in M2/M3.

## Run

```bash
npm install
npm run dev
```

`npm run build` regenerates TypeScript wire types from `PitWall.Contracts`, type-checks the client, and writes production static files to `dist/`. The build requires the .NET 10 SDK as well as Node.js. Run `npm run generate:types` directly when you only need to refresh the generated contracts.
