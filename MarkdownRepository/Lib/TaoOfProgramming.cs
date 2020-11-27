using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace MarkdownRepository.Lib
{
    public class TaoOfProgramming
    {
        public string en { get; set; }
        public string zh { get; set; }

        static List<TaoOfProgramming> All
        {
            get
            {
                if(HttpContext.Current != null                    
                    )
                {
                    if(HttpContext.Current.Cache["TaoOfProgramming"] != null)
                    {
                        return HttpContext.Current.Cache["TaoOfProgramming"] as List<TaoOfProgramming>;
                    }

                    var file = HttpContext.Current.Server.MapPath("~/Content/tao-of-programming.json");
                    var json = File.ReadAllText(file);
                    var list = JsonConvert.DeserializeObject<List<TaoOfProgramming>>(json);
                    HttpContext.Current.Cache.Insert("TaoOfProgramming", list);
                    return list;
                }

                return null;
            }
        }

        public static TaoOfProgramming GetNext()
        {
            if (HttpContext.Current == null) return null;
            
            var list = All;
            if (list == null) return null;

            var len = list.Count;
            var rand = new Random().Next(0, len-1);
            return list[rand];
        }
    }
}