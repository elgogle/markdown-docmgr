using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using System.Web.Mvc;
using System.ServiceModel.Syndication;

namespace MarkdownRepository.Lib
{
    public class RssResult : ActionResult
    {
        public SyndicationFeed Feed { get; set; }

        public RssResult() { }

        public RssResult(SyndicationFeed feed)
        {
            this.Feed = feed;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            context.HttpContext.Response.ContentType = "application/rss+xml";

            Rss20FeedFormatter formatter = new Rss20FeedFormatter(this.Feed);

            using (XmlWriter writer = XmlWriter.Create(context.HttpContext.Response.Output))
            {
                formatter.WriteTo(writer);
            }
        }
    }
}