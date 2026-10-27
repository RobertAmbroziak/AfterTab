using AfterTab.Common.Errors;

namespace AfterTab.Common.Validation;

public static class DisplayNames
{
    public static string Normalize(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
            throw new AppException(ErrorKind.Validation, "Podaj imię od 1 do 100 znaków, bez znaków sterujących.");
        return name;
    }
}
