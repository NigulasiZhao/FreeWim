using System.Security.Cryptography;
using System.Text;

namespace FreeWim.Utils;

public class AesHelper
{
    /// <summary>
    /// 默认密钥-密钥的长度必须是32
    /// </summary>
    private const string PublicKey = "0111068829800000";

    /// <summary>
    /// 默认向量
    /// </summary>
    private const string Iv = "0111068829800000";


    /// <summary>
    /// AES加密
    /// </summary>
    /// <param name="str">需要加密的字符串</param>
    /// <returns>加密后的字符串</returns>
    public static string EncryptAes(string str)
    {
        using (var aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes(PublicKey);
            aesAlg.IV = Encoding.UTF8.GetBytes(Iv);
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;

            var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using (var msEncrypt = new MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(str);
                    }

                    var encrypted = msEncrypt.ToArray();
                    // 通常，你可能希望将加密后的数据转换为Base64字符串以便于存储或传输
                    // 但这里我们直接返回字节数组的十六进制字符串表示
                    return ByteArrayToHexString(encrypted);
                }
            }
        }
    }

    /// <summary>
    /// AES解密
    /// </summary>
    /// <param name="str">需要解密的字符串</param>
    /// <param name="key">32位密钥</param>
    /// <returns>解密后的字符串</returns>
    public static string Decrypt(string str)
    {
        try
        {
            var keyArray = Encoding.UTF8.GetBytes(PublicKey);
            var toEncryptArray = HexStringToByteArray(str);
            using var aes = Aes.Create();
            aes.Key = keyArray;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = Encoding.UTF8.GetBytes(Iv);
            using var cTransform = aes.CreateDecryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Encoding.UTF8.GetString(resultArray);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Decryption failed: {e.Message}");
            return str;
        }
    }

    /// <summary>
    /// 将字节数组转换为十六进制字符串
    /// </summary>
    /// <param name="ba"></param>
    /// <returns></returns>
    private static string ByteArrayToHexString(byte[] ba)
    {
        var hex = new StringBuilder(ba.Length * 2);
        foreach (var b in ba) hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    /// <summary>
    /// 将指定的16进制字符串转换为byte数组
    /// </summary>
    /// <param name="s">16进制字符串(如：“7F 2C 4A”或“7F2C4A”都可以)</param>
    /// <returns>16进制字符串对应的byte数组</returns>
    private static byte[] HexStringToByteArray(string s)
    {
        s = s.Replace(" ", "");
        var buffer = new byte[s.Length / 2];
        for (var i = 0; i < s.Length; i += 2)
            buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
        return buffer;
    }
}