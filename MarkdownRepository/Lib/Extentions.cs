using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace MarkdownRepository.Lib
{
    public static class Extentions
    {
        const string DESKEY = "7JKFJ*&*JVN#@HGFNjfkwoYGGLdji";


        public static string GetFileSize(this long? length)
        {
            if (length == null) return "";
            var len = length.Value;

            if(len < 1024)
            {
                return len.ToString() + " byte";
            }
            else if(len < (1024 * 1024))
            {
                return (len / 1024M).ToString("N2") + " kb";
            }
            else
            {
                return (len / (1024M * 1024)).ToString("N2") + " mb";
            }
        }

        public static string GetUserName(this string fullName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                if (fullName.Any(t => t == '\\'))
                {
                    return fullName.Substring(fullName.LastIndexOf('\\') + 1);
                }

                return fullName;
            }

            return fullName;
        }

        const string HTML_TAG_PATTERN = "<.*?>";
        /// <summary>
        /// 去掉html 标签
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static string StripHTML(this string inputString)
        {
            return Regex.Replace
              (inputString, HTML_TAG_PATTERN, string.Empty);
        }

        /// <summary>
        /// 获取摘要, 取256个长度
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string GetShortDesc(this string content, int length=256)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            if (content.Length > length)
                return content.Substring(0, length);

            return content;
        }


        /// <summary>
        /// 是否为数值
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNum(this string str)
        {
            double num;
            return double.TryParse(str, out num);
        }

        /// <summary>
        /// 是否为空
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
        }

        /// <summary>
        /// 返回右边最多指定长度字符串
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Right(this string str, int length)
        {
            if (str.IsNullOrEmpty()) return "";
            str = str.Trim();
            return str.Length > length ? str.Substring(str.Length - length, length) : str;
        }

        /// <summary>
        /// 返回左边最多指定长度字符串
        /// </summary>
        /// <param name="str"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string Left(this string str, int length)
        {
            if (str.IsNullOrEmpty()) return "";

            str = str.Trim();
            return str.Length > length ? str.Substring(0, length) : str;
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string EncryByDES(this string str)
        {
            DESCryptoServiceProvider descryptoServiceProvider = new DESCryptoServiceProvider();
            descryptoServiceProvider.Key = Encoding.ASCII.GetBytes(DESKEY.Substring(0, 8));
            descryptoServiceProvider.IV = Encoding.ASCII.GetBytes(DESKEY.Substring(0, 8));
            byte[] bytes = Encoding.GetEncoding("GB2312").GetBytes(str);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, descryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(bytes, 0, bytes.Length);
            cryptoStream.FlushFinalBlock();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte b in memoryStream.ToArray())
            {
                stringBuilder.AppendFormat("{0:X2}", b);
            }
            memoryStream.Close();
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="encryptStr"></param>
        /// <returns></returns>
        public static string DecryByDES(this string encryptStr)
        {
            DESCryptoServiceProvider descryptoServiceProvider = new DESCryptoServiceProvider();
            descryptoServiceProvider.Key = Encoding.ASCII.GetBytes(DESKEY.Substring(0, 8));
            descryptoServiceProvider.IV = Encoding.ASCII.GetBytes(DESKEY.Substring(0, 8));
            checked
            {
                byte[] array = new byte[(int)Math.Round(unchecked((double)encryptStr.Length / 2.0 - 1.0)) + 1];
                int num = 0;
                int num2 = (int)Math.Round(unchecked((double)encryptStr.Length / 2.0 - 1.0));
                int num3 = num;
                for (;;)
                {
                    int num4 = num3;
                    int num5 = num2;
                    if (num4 > num5)
                    {
                        break;
                    }
                    int num6 = Convert.ToInt32(encryptStr.Substring(num3 * 2, 2), 16);
                    array[num3] = (byte)num6;
                    num3++;
                }
                MemoryStream memoryStream = new MemoryStream();
                CryptoStream cryptoStream = new CryptoStream(memoryStream, descryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Write);
                cryptoStream.Write(array, 0, array.Length);
                cryptoStream.FlushFinalBlock();
                memoryStream.Close();
                return Encoding.GetEncoding("GB2312").GetString(memoryStream.ToArray());
            }
        }

        /// <summary>
        /// 去掉 xml 中不受支持的字符
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string XmlCharacterEscape(this string text)
        {
            if (text == null) return null;

            StringBuilder sbOutput = new StringBuilder();
            char ch;

            for (int i = 0; i < text.Length; i++)
            {
                ch = text[i];
                if ((ch >= 0x0020 && ch <= 0xD7FF) ||
                        (ch >= 0xE000 && ch <= 0xFFFD) ||
                        ch == 0x0009 ||
                        ch == 0x000A ||
                        ch == 0x000D)
                {
                    sbOutput.Append(ch);
                }
            }

            return sbOutput.ToString();
        }

        /// <summary>
        /// 获取字符串的Hash字符
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToHashText(this string str)
        {
            if (String.IsNullOrEmpty(str))
                return String.Empty;

            using (var sha1 = new System.Security.Cryptography.SHA1Managed())
            {
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(str);

                byte[] hash = sha1.ComputeHash(textData);

                return BitConverter.ToString(hash).Replace("-", String.Empty);
            }
        }
    }
}