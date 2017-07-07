namespace CrystalGroup.ISD.DocumentManage
{
    using System.Web;
    using System.Web.Optimization;

    public class BundleConfig
    {
        // For more information on Bundling, visit http://go.microsoft.com/fwlink/?LinkId=254725
        public static void RegisterBundles(BundleCollection bundles)
        {
            var jqeury = new ScriptBundle("~/jquery")
                .Include("~/javascripts/jquery.min.js");

            var bootstrapJs = new ScriptBundle("~/bootstrap")
                .Include("~/javascripts/bootstrap.min.js");

            var editorMdScripts = new ScriptBundle("~/script/editor.md")                
                .Include("~/editor.md/editormd.js")
                .Include("~/editor.md/lib/codemirror/codemirror.min.js")
                .Include("~/editor.md/lib/codemirror/addons.min.js")
                .Include("~/editor.md/lib/codemirror/modes.min.js")
                .Include("~/editor.md/lib/marked.min.js")
                .Include("~/editor.md/lib/prettify.min.js")
                .Include("~/editor.md/lib/raphael.min.js")
                .Include("~/editor.md/lib/underscore.min.js")
                .Include("~/editor.md/lib/sequence-diagram.min.js")
                .Include("~/editor.md/lib/flowchart.min.js")
                .Include("~/editor.md/lib/jquery.flowchart.min.js");

            var editorMdStyle = new StyleBundle("~/css/editor.md")
                .Include("~/editor.md/lib/codemirror/codemirror.min.css")
                .Include("~/editor.md/lib/codemirror/addon/dialog/dialog.css")
                .Include("~/editor.md/lib/codemirror/addon/search/matchesonscrollbar.css")
                .Include("~/editor.md/lib/codemirror/codemirror.min.js")
                .Include("~/editor.md/css/editormd.css");
            
            bundles.Add(editorMdStyle);
            bundles.Add(jqeury);
            bundles.Add(bootstrapJs);
            bundles.Add(editorMdScripts);

            BundleTable.EnableOptimizations = true;
        }
    }
}