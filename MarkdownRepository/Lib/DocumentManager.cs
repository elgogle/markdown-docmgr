using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System.IO;
using Lucene.Net.Analysis.PanGu;

namespace MarkdownRepository.Lib
{
    public class Doc
    {
        public string Content { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Id { get; set; }
    }

    public class DocumentManager
    {
        public static readonly DocumentManager DocManager = new DocumentManager();
        const string INDEX_PATH = "~/App_Data/Index/";
        Queue<Doc> AddDocs = new Queue<Doc>();

        internal DocumentManager() { }

        struct DocStruct
        {
            public const string CONTENT = "content";
            public const string TITLE = "title";
            public const string CATEGORY = "category";
            public const string ID = "id";
        }

        private static string _IndexPath
        {
            get
            {
                return HttpContext.Current.Server.MapPath(INDEX_PATH);
            }
        }

        /// <summary>
        /// 删除一个文档
        /// </summary>
        /// <param name="docId"></param>
        public void DeleteDoc(string docId)
        {
            FSDirectory dir = FSDirectory.Open(new DirectoryInfo(_IndexPath), new NoLockFactory());
            DeleteDoc(dir, docId);
            dir.Close();
        }

        private void DeleteDoc(FSDirectory dir, string docId)
        {
            var reader = IndexReader.Open(dir, false);
            reader.DeleteDocuments(new Term(DocStruct.ID, docId));
            reader.Commit();
            reader.Close();
        }

        public bool ExistsDoc(string docId)
        {
            FSDirectory dir = FSDirectory.Open(new DirectoryInfo(_IndexPath), new NoLockFactory());
            bool isExistIndex = IndexReader.IndexExists(dir);

            if (isExistIndex)
            {
                var reader = IndexReader.Open(dir, true);
                IndexSearcher searcher = new IndexSearcher(reader);
                var q = new TermQuery(new Term(DocStruct.ID, docId));
                TopScoreDocCollector collector = TopScoreDocCollector.create(10,true);
                searcher.Search(q, collector);
                return collector.TopDocs().totalHits > 0;
            }
            else
                return false;
        }

        /// <summary>
        /// 增加或修改一个文档
        /// </summary>
        /// <param name="doc"></param>
        public void EditDoc(Doc doc)
        {
            FSDirectory dir = FSDirectory.Open(new DirectoryInfo(_IndexPath), new NoLockFactory());
            bool isExistIndex = IndexReader.IndexExists(dir);
            if (isExistIndex)
            {
                DeleteDoc(dir, doc.Id);            
            }
            
            IndexWriter writer = new IndexWriter(dir, new PanGuAnalyzer(), !isExistIndex, IndexWriter.MaxFieldLength.UNLIMITED);
            Document ndoc = CreateDocument(doc);
            writer.AddDocument(ndoc);
            writer.Commit();
            writer.Close();    
            dir.Close();
        }

        private static Document CreateDocument(Doc doc)
        {
            Document ndoc = new Document();
            ndoc.Add(new Field(DocStruct.TITLE, doc.Title, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            ndoc.Add(new Field(DocStruct.CONTENT, doc.Content, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            ndoc.Add(new Field(DocStruct.CATEGORY, doc.Category, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            ndoc.Add(new Field(DocStruct.ID, doc.Id, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            return ndoc;
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public List<Doc> Search(string text)
        {
            List<Doc> result = new List<Doc>();
            FSDirectory dir = FSDirectory.Open(new DirectoryInfo(_IndexPath), new NoLockFactory());
            bool isExistIndex = IndexReader.IndexExists(dir);
            
            if (isExistIndex)
            {
                var reader = IndexReader.Open(dir, true);
                IndexSearcher searcher = new IndexSearcher(reader);

                //搜索条件
                BooleanQuery queryOr = new BooleanQuery();
                PhraseQuery query = new PhraseQuery();
                //把用户输入的关键字进行分词
                foreach (string word in SplitContent.SplitWords(text))
                {
                    query.Add(new Term(DocStruct.CONTENT, word));
                    queryOr.Add(query, BooleanClause.Occur.SHOULD);//这里设置 条件为Or关系
                }
                query.SetSlop(100); //指定关键词相隔最大距离

                //TopScoreDocCollector盛放查询结果的容器
                TopScoreDocCollector collector = TopScoreDocCollector.create(1000, true);
                //searcher.Search(query, null, collector);//根据query查询条件进行查询，查询结果放入collector容器
                searcher.Search(queryOr, null, collector);
                //TopDocs 指定0到GetTotalHits() 即所有查询结果中的文档 如果TopDocs(20,10)则意味着获取第20-30之间文档内容 达到分页的效果
                ScoreDoc[] docs = collector.TopDocs(0, collector.GetTotalHits()).scoreDocs;
                for (int i = 0; i < docs.Length; i++)
                {
                    int docId = docs[i].doc;//得到查询结果文档的id（Lucene内部分配的id）
                    Document doc = searcher.Doc(docId);//根据文档id来获得文档对象Document
                    var d = new Doc();
                    d.Id = doc.Get(DocStruct.ID);
                    result.Add(d);
                }

                reader.Close();
            }

            dir.Close();

            return result;
        }
       
    }
}