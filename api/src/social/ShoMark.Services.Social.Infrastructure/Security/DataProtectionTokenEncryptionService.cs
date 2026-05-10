using Microsoft.AspNetCore.DataProtection;
using ShoMark.Application.Interfaces;

namespace ShoMark.Infrastructure.Security;

public class DataProtectionTokenEncryptionService : ITokenEncryptionService
{
    private readonly IDataProtector _protector;

    public DataProtectionTokenEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ShoMark.Tokens.v1");
    }

    public string Encrypt(string plainText)
    {
        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        return _protector.Unprotect(cipherText);
    }
}
