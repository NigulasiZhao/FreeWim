using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FreeWim.Models;
using Newtonsoft.Json.Linq;

namespace FreeWim.Services;

public class JwtService(ILogger<JwtService> logger)
{
    /// <summary>
    /// 解析JWT Token，返回JwtTokenInfo对象
    /// </summary>
    /// <param name="token">JWT Token字符串</param>
    /// <returns>解析后的JwtTokenInfo对象，失败返回null</returns>
    public JwtTokenInfo? ParseToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("Token为空，无法解析");
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var tokenInfo = new JwtTokenInfo
            {
                Iat = GetClaimAsLong(jwtToken, "iat"),
                Id = GetClaimValue(jwtToken, "id"),
                JwtId = GetClaimValue(jwtToken, "jwtId"),
                Uid = GetClaimValue(jwtToken, "uid"),
                TenantId = GetClaimValue(jwtToken, "tenantId"),
                Cid = GetClaimValue(jwtToken, "cid"),
                MainId = GetClaimValue(jwtToken, "mainId"),
                Avatar = GetClaimValue(jwtToken, "avatar"),
                Name = GetClaimValue(jwtToken, "name"),
                Account = GetClaimValue(jwtToken, "account"),
                Mobile = GetClaimValue(jwtToken, "mobile"),
                Sn = GetClaimValue(jwtToken, "sn"),
                Group = GetClaimValue(jwtToken, "group"),
                GroupName = GetClaimValue(jwtToken, "groupName"),
                YhloNum = GetClaimValue(jwtToken, "yhloNum"),
                IsAdmin = GetClaimAsBool(jwtToken, "isAdmin"),
                Channel = GetClaimValue(jwtToken, "channel"),
                TokenFrom = GetClaimValue(jwtToken, "tokenfrom"),
                UserType = GetClaimValue(jwtToken, "userType"),
                Exp = GetClaimAsLong(jwtToken, "exp")
            };

            // 解析roles数组
            var rolesClaim = GetClaimValue(jwtToken, "roles");
            if (!string.IsNullOrEmpty(rolesClaim))
            {
                try
                {
                    var rolesArray = JArray.Parse(rolesClaim);
                    tokenInfo.Roles = rolesArray.Select(r => r.ToString()).ToList();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "解析roles失败: {RolesClaim}", rolesClaim);
                }
            }

            // 解析company对象
            var companyClaim = GetClaimValue(jwtToken, "company");
            if (!string.IsNullOrEmpty(companyClaim))
            {
                try
                {
                    var companyObj = JObject.Parse(companyClaim);
                    tokenInfo.Company = new CompanyInfo
                    {
                        Id = companyObj["id"]?.ToString() ?? string.Empty,
                        Name = companyObj["name"]?.ToString() ?? string.Empty,
                        Code = companyObj["code"]?.ToString() ?? string.Empty
                    };
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "解析company失败: {CompanyClaim}", companyClaim);
                }
            }

            logger.LogDebug("JWT Token解析成功，MainId: {MainId}, Name: {Name}", tokenInfo.MainId, tokenInfo.Name);
            return tokenInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "解析JWT Token失败");
            return null;
        }
    }

    /// <summary>
    /// 从JWT Token中获取指定的Claim值
    /// </summary>
    private string GetClaimValue(JwtSecurityToken token, string claimType)
    {
        return token.Claims.FirstOrDefault(c => c.Type == claimType)?.Value ?? string.Empty;
    }

    /// <summary>
    /// 从JWT Token中获取Long类型的Claim值
    /// </summary>
    private long GetClaimAsLong(JwtSecurityToken token, string claimType)
    {
        var value = GetClaimValue(token, claimType);
        return long.TryParse(value, out var result) ? result : 0;
    }

    /// <summary>
    /// 从JWT Token中获取Bool类型的Claim值
    /// </summary>
    private bool GetClaimAsBool(JwtSecurityToken token, string claimType)
    {
        var value = GetClaimValue(token, claimType);
        return bool.TryParse(value, out var result) && result;
    }

    /// <summary>
    /// 快速获取MainId（常用场景）
    /// </summary>
    public string? GetMainId(string? token)
    {
        var tokenInfo = ParseToken(token);
        return tokenInfo?.MainId;
    }

    /// <summary>
    /// 快速获取用户手机号（常用场景）
    /// </summary>
    public string? GetMobile(string? token)
    {
        var tokenInfo = ParseToken(token);
        return tokenInfo?.Mobile;
    }
}
