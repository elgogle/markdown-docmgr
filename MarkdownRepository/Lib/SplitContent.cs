using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.PanGu;
using PanGu;
using System.Linq;
using Reg = System.Text.RegularExpressions;
using System;

namespace MarkdownRepository.Lib
{
    public static class SplitContent
    {
        public static string[] SplitWords(string content)
        {
            List<string> strList = new List<string>();
            Analyzer analyzer = new PanGuAnalyzer();//指定使用盘古 PanGuAnalyzer 分词算法
            TokenStream tokenStream = analyzer.TokenStream("", new StringReader(content));
            Lucene.Net.Analysis.Token token = null;
            while ((token = tokenStream.Next()) != null)
            { //Next继续分词 直至返回null
                strList.Add(token.TermText()); //得到分词后结果
            }
            return strList.ToArray();
        }

        //需要添加PanGu.HighLight.dll的引用
        /// <summary>
        /// 搜索结果高亮显示
        /// </summary>
        /// <param name="keyword"> 关键字 </param>
        /// <param name="content"> 搜索结果 </param>
        /// <returns> 高亮后结果 </returns>
        public static string HightLight(string keyword, string content)
        {
            //创建HTMLFormatter,参数为高亮单词的前后缀
            PanGu.HighLight.SimpleHTMLFormatter simpleHTMLFormatter =
                new PanGu.HighLight.SimpleHTMLFormatter("<font style=\"font-style:normal;color:#cc0000;\"><b>", "</b></font>");
            //创建 Highlighter ，输入HTMLFormatter 和 盘古分词对象Semgent
            PanGu.HighLight.Highlighter highlighter =
                            new PanGu.HighLight.Highlighter(simpleHTMLFormatter,
                            new Segment());
            //设置每个摘要段的字符数
            highlighter.FragmentSize = 256;
            //获取最匹配的摘要段
            var result = highlighter.GetBestFragment(keyword, content);
            if (!string.IsNullOrWhiteSpace(result))
                return result;
            return content.Left(200);
        }

        public static bool IsWord(this string text)
        {
            //PanGu.Segment.Init();
            //var seg = new PanGu.Segment();
            //var rst = seg.DoSegment(text, new PanGu.Match.MatchOptions { FilterNumeric = true, FrequencyFirst = true, EnglishSegment = true });
            //var a = rst.OrderByDescending(t => t.Frequency).FirstOrDefault();
            //return a != null && a.Frequency > 0;

            Reg.Regex regChina = new Reg.Regex("^[^\x00-\xFF]");
            Reg.Regex regEnglish = new Reg.Regex("^[a-zA-Z]");
            return (regChina.IsMatch(text) || regEnglish.IsMatch(text)) && text.Length > 1;
        }

        public static List<Tuple<string, double>> GetTermFreq(this string text)
        {
            Segment.Init();
            var seg = new PanGu.Segment();
            var rst = seg.DoSegment(text, new PanGu.Match.MatchOptions
            {
                FilterNumeric = true,
                FrequencyFirst = true,
                //EnglishSegment = true,
                IgnoreCapital = true,
            });
            return rst.Where(t => t.Word.Length > 1)
                .Select(t => new Tuple<string, double>(t.Word, 1))
                .ToList();
        }

        public static List<Tuple<string, double>> GetWordFreq(this IEnumerable<string> article)
        {
            var rst = new List<Tuple<string, double>>();
            foreach (var w in article)
            {
                rst.AddRange(w.GetTermFreq());
            }

            return rst
               .GroupBy(g => g.Item1)
               .Select(t => new Tuple<string, double>(t.Key, t.Sum(i => i.Item2)))
               .OrderByDescending(t => t.Item2)
               .Take(100)
               .ToList();
        }
    }
}