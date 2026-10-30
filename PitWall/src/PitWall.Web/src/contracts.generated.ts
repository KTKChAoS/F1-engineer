// Generated from PitWall.Contracts. Do not edit by hand.

export type CallPreset = "BoxThisLap" | "BoxNextLap" | "StayOut" | "PushNow" | "ManageTyres" | "SaveFuel" | "MaxErs" | "CarBehindClosing" | "GoodLap" | "FreeText";
export type CallStatus = "Sent" | "Delivered" | "Acknowledged";
export type ClientRole = "Engineer" | "Driver";
export type ConnectionState = "Waiting" | "Receiving" | "Stale";
export type DriverStatus = "InGarage" | "FlyingLap" | "InLap" | "OutLap" | "OnTrack";
export type DrsState = "Off" | "Available" | "Active";
export type ErsMode = "None" | "Medium" | "Hotlap" | "Overtake";
export type FiaFlag = "None" | "Green" | "Blue" | "Yellow";
export type FuelMix = "Lean" | "Standard" | "Rich" | "Max";
export type PitStatus = "None" | "Pitting" | "InPitArea";
export type ResultStatus = "Invalid" | "Inactive" | "Active" | "Finished" | "DidNotFinish" | "Disqualified" | "NotClassified" | "Retired";
export type SafetyCarStatus = "None" | "SC" | "VSC" | "Formation";
export type SessionType = "Unknown" | "Practice1" | "Practice2" | "Practice3" | "ShortPractice" | "Qualifying1" | "Qualifying2" | "Qualifying3" | "ShortQualifying" | "OneShotQualifying" | "SprintShootout1" | "SprintShootout2" | "SprintShootout3" | "ShortSprintShootout" | "OneShotSprintShootout" | "Race" | "Race2" | "Race3" | "TimeTrial";
export type TyreCompound = "Unknown" | "Soft" | "Medium" | "Hard" | "Inter" | "Wet";
export type Weather = "Clear" | "LightCloud" | "Overcast" | "LightRain" | "HeavyRain" | "Storm";

export interface CallAckMessage {
  "callId": string;
}

export interface CallMessage {
  "callId": string;
  "preset": CallPreset;
  "text": string;
}

export interface CallStatusMessage {
  "callId": string;
  "status": CallStatus;
}

export interface CarInfo {
  "index": number;
  "name": string;
  "teamId": number;
  "teamColor": string;
  "raceNumber": number;
  "position": number;
  "gridPosition": number;
  "gapLeaderMs": number;
  "intervalMs": number;
  "lastLapMs": number;
  "bestLapMs": number;
  "compound": TyreCompound;
  "tyreAgeLaps": number;
  "pitStatus": PitStatus;
  "stops": number;
  "penaltiesS": number;
  "driverStatus": DriverStatus;
  "resultStatus": ResultStatus;
  "isPlayer": boolean;
  "isAi": boolean;
}

export interface CarSetupInfo {
  "frontWing": number;
  "rearWing": number;
  "onThrottle": number;
  "offThrottle": number;
  "frontCamber": number;
  "rearCamber": number;
  "frontToe": number;
  "rearToe": number;
  "frontSuspension": number;
  "rearSuspension": number;
  "frontAntiRollBar": number;
  "rearAntiRollBar": number;
  "frontSuspensionHeight": number;
  "rearSuspensionHeight": number;
  "brakePressure": number;
  "brakeBias": number;
  "engineBraking": number;
  "tyrePressures": number[];
  "ballast": number;
  "fuelLoad": number;
}

export interface ConnectionInfo {
  "state": ConnectionState;
  "pps": number;
  "packetFormat": number;
}

export interface CreateRoomMessage {
  "clientVersion": string;
  "hostName": string;
}

export interface DamageInfo {
  "frontWingL": number;
  "frontWingR": number;
  "rearWing": number;
  "floor": number;
  "diffuser": number;
  "sidepod": number;
  "drsFault": boolean;
  "ersFault": boolean;
  "gearbox": number;
  "engine": number;
  "engineMguh": number;
  "engineEs": number;
  "engineCe": number;
  "engineIce": number;
  "engineMguk": number;
  "engineTc": number;
  "engineBlown": boolean;
  "engineSeized": boolean;
}

export interface DerivedMetrics {
  "lapsRemaining": number;
  "tyreWearRate"?: number[];
  "tyreLapsToThreshold"?: number[];
  "fuelBurnPerLap"?: number | null;
  "fuelSurplusLaps"?: number | null;
  "gapAheadTrend"?: number | null;
  "gapBehindTrend"?: number | null;
  "gapBehindUnder1sLaps": number;
  "pitLossS": number;
  "undercutHint": boolean;
}

export interface DriverInfo {
  "speedKph": number;
  "gear": number;
  "rpm": number;
  "maxRpm": number;
  "throttle": number;
  "brake": number;
  "steer": number;
  "drs": DrsState;
}

export interface EnergyInfo {
  "fuelKg": number;
  "fuelLaps": number;
  "fuelMix": FuelMix;
  "ersPct": number;
  "ersMode": ErsMode;
  "ersHarvestedMJ": number;
  "ersDeployedMJ": number;
}

export interface ErrorMessage {
  "code": string;
  "message": string;
}

export interface EventRecord {
  "id": number;
  "code": string;
  "lap": number;
  "carIndex": number;
  "otherCarIndex": number;
  "text": string;
}

export interface FieldPayload {
  "session": SessionInfo;
  "forecast": ForecastSample[];
  "trackPath": MapPoint[];
  "positionHistory": PositionHistoryRecord[];
  "gapHistory": GapHistoryRecord[];
  "setup"?: CarSetupInfo;
  "cars": CarInfo[];
  "tyreInfo": TyreDetailInfo;
  "damage": DamageInfo;
  "laps": LapRecord[];
  "strategy": StrategyInfo;
  "derived": DerivedMetrics;
  "events": EventRecord[];
}

export interface ForecastSample {
  "offsetMin": number;
  "weather": Weather;
  "trackTempC": number;
  "airTempC": number;
  "rainPct": number;
}

export interface GapHistoryRecord {
  "lapNo": number;
  "aheadMs": number;
  "behindMs"?: number | null;
}

export interface HelloMessage {
  "role": ClientRole;
  "name": string;
  "clientVersion": string;
}

export interface JoinApproveMessage {
  "requestId": string;
}

export interface JoinDenyMessage {
  "requestId": string;
}

export interface JoinRequestMessage {
  "requestId": string;
  "name": string;
}

export interface KickViewerMessage {
  "viewerId": string;
}

export interface LapRecord {
  "lapNo": number;
  "lapMs": number;
  "s1Ms": number;
  "s2Ms": number;
  "s3Ms": number;
  "valid": boolean;
  "compound": TyreCompound;
  "wearPct": number[];
  "fuelKg": number;
}

export interface LivePayload {
  "conn": ConnectionInfo;
  "player": PlayerInfo;
  "driver": DriverInfo;
  "tyres": TyreInfo;
  "energy": EnergyInfo;
}

export interface MapPoint {
  "x": number;
  "z": number;
}

export interface PingMessage {
  "ts": number;
}

export interface PitWindowInfo {
  "ideal": number;
  "latest": number;
}

export interface PlayerInfo {
  "carIndex": number;
  "position": number;
  "lap": number;
  "sector": number;
  "currentLapMs": number;
  "lastLapMs": number;
  "bestLapMs": number;
  "gapAheadMs": number;
  "gapBehindMs": number;
  "gapLeaderMs": number;
  "pitStatus": PitStatus;
  "invalidLap": boolean;
}

export interface PongMessage {
  "ts": number;
  "serverTs": number;
}

export interface PositionHistoryRecord {
  "lapNo": number;
  "positions": number[];
}

export interface RoomClosedMessage {
  "reason": string;
}

export interface RoomCreatedMessage {
  "roomCode": string;
  "hostSecret": string;
  "joinUrl": string;
  "expiresAt": number;
}

export interface RoomStatusMessage {
  "roomCode": string;
  "joinUrl": string;
  "viewerCount": number;
  "approvalRequired": boolean;
  "callsAllowed": boolean;
}

export interface SessionInfo {
  "uid": number;
  "type": SessionType;
  "track": string;
  "trackLengthM": number;
  "totalLaps": number;
  "currentLap": number;
  "timeLeftS": number;
  "weather": Weather;
  "trackTempC": number;
  "airTempC": number;
  "safetyCar": SafetyCarStatus;
  "flag": FiaFlag;
  "pitWindow": PitWindowInfo;
  "paused": boolean;
}

export interface StintInfo {
  "startLap": number;
  "endLap": number;
  "compound": TyreCompound;
}

export interface StrategyInfo {
  "stints": StintInfo[];
  "tyreSets": TyreSetInfo[];
}

export interface TyreDetailInfo {
  "compoundActual": TyreCompound;
  "compoundVisual": TyreCompound;
  "ageLaps": number;
  "damagePct": number[];
}

export interface TyreInfo {
  "surfaceC": number[];
  "innerC": number[];
  "pressurePsi": number[];
  "brakeC": number[];
  "wearPct": number[];
}

export interface TyreSetInfo {
  "compound": TyreCompound;
  "wearPct": number;
  "lifeLeft": number;
  "available": boolean;
  "fitted": boolean;
}

export interface ViewerInfo {
  "id": string;
  "name": string;
  "role": ClientRole;
  "connected": boolean;
}

export interface ViewerListMessage {
  "viewers": ViewerInfo[];
}

export interface WireMessage<T> {
  "type": string;
  "seq": number;
  "t": number;
  "payload": T;
}

