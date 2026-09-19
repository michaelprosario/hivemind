using FileRepo.Core.Common;
namespace FileRepo.Core.Files;
public static class FileNameValidator
{
    public static IReadOnlyList<ValidationError> Validate(string? name, FileValidationRules rules)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(name)) { errors.Add(new("fileName", "A file name is required.")); return errors; }
        if (name.Length > rules.MaxNameLength) errors.Add(new("fileName", $"File name cannot exceed {rules.MaxNameLength} characters."));
        if (name is "." or ".." || name.StartsWith('.') || name != Path.GetFileName(name) || name.Contains('/') || name.Contains('\\'))
            errors.Add(new("fileName", "File name must be a single non-hidden file name."));
        if (name.Any(c => Path.GetInvalidFileNameChars().Contains(c))) errors.Add(new("fileName", "File name contains invalid characters."));
        if (name.EndsWith('.') || name.EndsWith(' ')) errors.Add(new("fileName", "File name cannot end with a dot or space."));
        return errors;
    }
}
