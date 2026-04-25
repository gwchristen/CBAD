namespace CBAD.Simulation;

internal sealed record SimBattery(int Station, int ProcessCode, int BaseVoltage, int Current, int Temp, string Health);
