using Conciliador.Application.Abstractions;
using Conciliador.Application.Common;
using FluentValidation;

namespace Conciliador.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, string Name, string Email);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.").EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha.");
    }
}

public sealed class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IValidator<LoginRequest> _validator;

    public LoginUseCase(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IValidator<LoginRequest> validator)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _validator = validator;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);

        // Mesma resposta para usuario inexistente e senha errada, para nao revelar quais e-mails existem
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var token = _tokens.CreateToken(user);
        return new LoginResponse(token.Token, token.ExpiresAtUtc, user.Name, user.Email);
    }
}
