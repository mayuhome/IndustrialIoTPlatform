using Application.Auth.Models;
using Domain;

namespace Application.Abstractions;

public interface ITokenIssuer
{
    AuthTokenResult Issue(User user);
}
