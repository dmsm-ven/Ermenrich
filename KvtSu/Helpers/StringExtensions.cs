using System.Text;

namespace KvtSu.Helpers;
public static class StringEx
{
    public static string TrimHtml(this string s)
    {
        if (s == null)
        {
            return s;
        }
        return s.Trim('\r', '\n', '\t', ' ');
    }

    public static string CreateMD5(this string input)
    {
        // Use input string to calculate MD5 hash
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString().ToLower();
        }
    }

    public static StringBuilder AppendTab(this StringBuilder sb, string text)
    {
        return sb.Append(text).Append("\t");
    }
}
