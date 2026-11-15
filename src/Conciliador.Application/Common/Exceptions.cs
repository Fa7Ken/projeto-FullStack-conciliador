namespace Conciliador.Application.Common;

// Planilha fora do formato esperado; a mensagem vai direto para o usuario
public class SpreadsheetFormatException : Exception
{
    public SpreadsheetFormatException(string fileName, string message) : base(message)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("E-mail ou senha inválidos.") { }
}
