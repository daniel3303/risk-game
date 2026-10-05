namespace Risk.Server.Models;

public sealed record Welcome(string Code, string Token, int Seat, RoomSnapshot Snapshot);
