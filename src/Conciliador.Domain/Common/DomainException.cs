namespace Conciliador.Domain.Common;

// Erro de regra de negocio; a API converte em resposta 400
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
