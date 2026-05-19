namespace LSPDFRManager.LocalApi.Dtos;

public record LogFileInfoDto(string Name, string Label);
public record LogsAvailableResponse(IReadOnlyList<LogFileInfoDto> Logs);
public record LogLinesResponse(string Name, string Label, IReadOnlyList<string> Lines, int TotalLines);
