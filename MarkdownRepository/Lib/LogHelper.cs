using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

[assembly: log4net.Config.DOMConfigurator(ConfigFile = "web.config", Watch = true)]
namespace CrystalGroup.ISD.DocumentManage.Lib
{
    public class LogHelper
    {
        public static void WriteError(Type t, Exception ex)
        {
            log4net.ILog log = log4net.LogManager.GetLogger(t);
            log.Error("Error", ex);
        }

        public static void WriteInfo(Type t, string message)
        {
            log4net.ILog log = log4net.LogManager.GetLogger(t);
            log.Info(message);
        }
    }
}