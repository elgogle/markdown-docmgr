#region Imports (18)

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.PanGu;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

#endregion Imports (18)

namespace MarkdownRepository.Lib
{
    public enum Operate
    {
        AddOrUpdate,
        Delete
    }

    public class Doc
    {
        #region Properties of Doc (5)

        public string Category { get; set; }

        public string Content { get; set; }

        public string Id { get; set; }

        public Operate Operate { get; set; }

        public string Title { get; set; }

        #endregion Properties of Doc (5)
    }

    public class IndexManager : IDisposable
    {
        #region Structs of IndexManager (1)

        struct DocStruct
        {
            #region Members of DocStruct (4)

            public const string CONTENT = "content";
            public const string TITLE = "title";
            public const string CATEGORY = "category";
            public const string ID = "id";

            #endregion Members of DocStruct (4)
        }

        #endregion Structs of IndexManager (1)

        #region Members of IndexManager (7)
        private static IndexManager _indexMgr = null;
        private static object _lock = new object();
        private Queue<Doc> _docqueue = new Queue<Doc>();
        private FSDirectory _fsDir = null;
        private IndexReader _indexReader = null;
        private IndexWriter _indexWriter = null;
        public static string IndexPath = null;

        #endregion Members of IndexManager (7)

        #region Properties of IndexManager (1)

        public static IndexManager IndexMgr
        {
            get
            {
                if (_indexMgr == null && !string.IsNullOrWhiteSpace(IndexPath))
                {
                    lock (_lock)
                    {
                        if (_indexMgr == null)
                            _indexMgr = new IndexManager();
                    }
                }

                return _indexMgr;
            }
        }

        #endregion Properties of IndexManager (1)

        #region Constructors of IndexManager (1)

        private IndexManager()
        {
            if (!System.IO.Directory.Exists(IndexPath))
                System.IO.Directory.CreateDirectory(IndexPath);
            this._fsDir = FSDirectory.Open(new DirectoryInfo(IndexPath), new NoLockFactory());

            this.Start();
        }

        #endregion Constructors of IndexManager (1)

        #region Methods of IndexManager (9)

        /// <summary>
        /// 增加或修改一个文档
        /// </summary>
        /// <param name="doc"></param>
        private void AddOrUpdate(Doc doc)
        {
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);
                if (isExistIndex)
                {
                    Delete(doc.Id);
                }

                Document ndoc = CreateDocument(doc);

                this._indexWriter = new IndexWriter(this._fsDir, new PanGuAnalyzer(), !isExistIndex, IndexWriter.MaxFieldLength.UNLIMITED);
                this._indexWriter.AddDocument(ndoc);
                this._indexWriter.Commit();
                this._indexWriter.Optimize();
                this._indexWriter.Close();
                //LogHelper.WriteInfo(this.GetType(), string.Format("indexing doc {0} completed.", doc.Id));
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        /// <summary>
        /// 添加文档索引或更新
        /// </summary>
        /// <param name="doc"></param>
        public void AddOrUpdateDocIndex(Doc doc)
        {
            this._docqueue.Enqueue(doc);
            //LogHelper.WriteInfo(this.GetType(), string.Format("current queue length {0}", this._docqueue.Count));
        }

        /// <summary>
        /// 创建文档
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
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
        /// 删除文档索引
        /// </summary>
        /// <param name="docId"></param>
        private void Delete(string docId)
        {
            try
            {
                this._indexReader = IndexReader.Open(this._fsDir, false);
                this._indexReader.DeleteDocuments(new Term(DocStruct.ID, docId));
                this._indexReader.Commit();
                this._indexReader.Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        public void Dispose()
        {
            this._fsDir.Close();
            this._indexReader = null;
            this._indexWriter = null;
            this._fsDir = null;
        }

        /// <summary>
        /// 检查文档索引是否存在
        /// </summary>
        /// <param name="docId"></param>
        /// <returns></returns>
        public bool Exists(string docId)
        {
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);

                if (isExistIndex)
                {
                    this._indexReader = IndexReader.Open(this._fsDir, false);
                    IndexSearcher searcher = new IndexSearcher(this._indexReader);
                    var q = new TermQuery(new Term(DocStruct.ID, docId));
                    TopScoreDocCollector collector = TopScoreDocCollector.create(10, true);
                    searcher.Search(q, collector);
                    return collector.TopDocs().totalHits > 0;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
                return false;
            }
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public List<Doc> Search(string text)
        {
            List<Doc> result = new List<Doc>();
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);

                if (isExistIndex)
                {
                    this._indexReader = IndexReader.Open(this._fsDir, false);
                    IndexSearcher searcher = new IndexSearcher(this._indexReader);

                    //搜索条件
                    BooleanQuery shouldQuery = new BooleanQuery();

                    //把用户输入的关键字进行分词
                    PhraseQuery query1 = new PhraseQuery();
                    PhraseQuery query2 = new PhraseQuery();
                    PhraseQuery query3 = new PhraseQuery();
                    foreach (string word in SplitContent.SplitWords(text))
                    {
                        query1.Add(new Term(DocStruct.TITLE, word));
                        query2.Add(new Term(DocStruct.CONTENT, word));                        
                        query3.Add(new Term(DocStruct.CATEGORY, word));
                    }
                    query1.SetBoost(0.4f);
                    query2.SetBoost(0.3f);
                    query3.SetBoost(0.2f);
                    shouldQuery.Add(query1, BooleanClause.Occur.SHOULD);
                    shouldQuery.Add(query2, BooleanClause.Occur.SHOULD);
                    shouldQuery.Add(query3, BooleanClause.Occur.SHOULD);

                    MultiSearcher multiSearch = new MultiSearcher(new[] { searcher });

                    //TopScoreDocCollector盛放查询结果的容器
                    TopScoreDocCollector collector = TopScoreDocCollector.create(300, true);

                    multiSearch.Search(shouldQuery, collector);

                    //TopDocs 指定0到GetTotalHits() 即所有查询结果中的文档 如果TopDocs(20,10)则意味着获取第20-30之间文档内容 达到分页的效果
                    ScoreDoc[] docs = collector.TopDocs(0, collector.GetTotalHits()).scoreDocs.OrderByDescending(t => t.score).ToArray();
                    for (int i = 0; i < docs.Length; i++)
                    {
                        int docId = docs[i].doc;//得到查询结果文档的id（Lucene内部分配的id）
                        Document doc = searcher.Doc(docId);//根据文档id来获得文档对象Document
                        var d = new Doc();
                        d.Id = doc.Get(DocStruct.ID);
                        result.Add(d);
                    }
                    //LogHelper.WriteInfo(this.GetType(), string.Format("Searched results count:{0}", docs.Length));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }

            return result;
        }

        /// <summary>
        /// 搜索文档内容且精确匹配
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public List<Doc> SearchDocumentContentAndNotSplitWord(string text)
        {
            List<Doc> result = new List<Doc>();
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);

                if (isExistIndex)
                {
                    this._indexReader = IndexReader.Open(this._fsDir, false);
                    IndexSearcher searcher = new IndexSearcher(this._indexReader);

                    //搜索条件
                    BooleanQuery queryOr1 = new BooleanQuery();
                    PhraseQuery query1 = new PhraseQuery();

                    foreach (string word in SplitContent.SplitWords(text))
                    {
                        query1.Add(new Term(DocStruct.CONTENT, word));
                        queryOr1.Add(query1, BooleanClause.Occur.MUST);
                    }

                    MultiSearcher multiSearch = new MultiSearcher(new[] { searcher });

                    //TopScoreDocCollector盛放查询结果的容器
                    TopScoreDocCollector collector = TopScoreDocCollector.create(300, true);
                    multiSearch.Search(queryOr1, collector);

                    ScoreDoc[] docs = collector.TopDocs(0, collector.GetTotalHits()).scoreDocs.OrderByDescending(t => t.score).ToArray();
                    for (int i = 0; i < docs.Length; i++)
                    {
                        int docId = docs[i].doc;//得到查询结果文档的id（Lucene内部分配的id）
                        Document doc = searcher.Doc(docId);//根据文档id来获得文档对象Document
                        var d = new Doc();
                        d.Id = doc.Get(DocStruct.ID);
                        result.Add(d);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }

            return result;
        }

        /// <summary>
        /// 开启索引编制
        /// </summary>
        private void Start()
        {
            Task task = new TaskFactory().StartNew(() =>
            {
                while (true)
                {
                    while (this._docqueue.Count > 0)
                    {
                        var doc = this._docqueue.Dequeue();
                        switch (doc.Operate)
                        {
                            case Operate.Delete:
                                Delete(doc.Id);
                                break;
                            case Operate.AddOrUpdate:
                                AddOrUpdate(doc);
                                break;
                        }
                    }

                    System.Threading.Thread.Sleep(2000);
                }
            });
        }

        #endregion Methods of IndexManager (9)
    }
}
