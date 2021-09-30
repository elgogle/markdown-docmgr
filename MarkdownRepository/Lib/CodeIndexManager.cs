#region Imports (11)

using Lucene.Net.Analysis.PanGu;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

#endregion Imports (11)

namespace MarkdownRepository.Lib
{
    public class CodeFile : IDoc
    {
        #region Properties of CodeFile (2)

        public string FileContent { get; set; }

        public string Id { get; set; }

        #endregion Properties of CodeFile (2)
    }

    public class CodeIndex : IDoc
    {
        #region Properties of CodeIndex (8)

        public string CodeBody { get; set; }

        public string FileContent { get; set; }

        public string FileId { get; set; }

        public string Id { get; set; }

        public string Language { get; set; }

        public Operate Operate { get; set; }

        public string SearchText { get; set; }

        public string UserId { get; set; }

        #endregion Properties of CodeIndex (8)
    }

    public class CodeIndexManager
    {
        #region Structs of CodeIndexManager (1)

        struct IndexField
        {
            #region Members of IndexField (7)

            public const string Id = "ID";
            public const string SearchText = "SearchText";
            public const string CodeBody = "CodeBody";
            public const string UserId = "UserId";
            public const string Language = "Language";
            public const string FileId = "FileId";
            public const string FileContent = "FileContent";

            #endregion Members of IndexField (7)
        }

        #endregion Structs of CodeIndexManager (1)

        #region Members of CodeIndexManager (5)
        private static object _lock = new object();
        private static CodeIndexManager _indexMgr = null;
        private Queue<CodeIndex> _docqueue = new Queue<CodeIndex>();
        private LuceneHelper<CodeFile> _codeFileLucene = null;
        private LuceneHelper<CodeIndex> _codeIndexLucene = null;

        #endregion Members of CodeIndexManager (5)

        #region Constructors of CodeIndexManager (1)

        private CodeIndexManager(string indexPath)
        {
            if (!System.IO.Directory.Exists(indexPath))
                System.IO.Directory.CreateDirectory(indexPath);

            indexPath = Path.GetDirectoryName(indexPath);

            this._codeIndexLucene = new LuceneHelper<CodeIndex>(indexPath);
            this._codeFileLucene = new LuceneHelper<CodeFile>(indexPath);
            this.Start();
        }

        #endregion Constructors of CodeIndexManager (1)

        #region Methods of CodeIndexManager (8)

        private void Create(CodeIndex m)
        {
            try
            {
                if (!this._codeIndexLucene.ExistsDoc(m.Id, IndexField.Id))
                {
                    Document doc = new Document();
                    doc.Add(this._codeIndexLucene.CreateStoreField(IndexField.Id, m.Id));
                    doc.Add(this._codeIndexLucene.CreateSearchField(IndexField.SearchText, m.SearchText));
                    doc.Add(this._codeIndexLucene.CreateStoreField(IndexField.Language, m.Language));
                    doc.Add(this._codeIndexLucene.CreateStoreField(IndexField.CodeBody, m.CodeBody));
                    doc.Add(this._codeIndexLucene.CreateStoreField(IndexField.UserId, m.UserId));
                    doc.Add(this._codeIndexLucene.CreateStoreField(IndexField.FileId, m.FileId));
                    this._codeIndexLucene.AddDoc(doc);
                }

                if (!m.FileId.IsNullOrEmpty()
                    && !this._codeFileLucene.ExistsDoc(m.FileId))
                {
                    Document codeFileDoc = new Document();
                    codeFileDoc.Add(this._codeIndexLucene.CreateStoreField(nameof(CodeFile.Id), m.FileId));
                    codeFileDoc.Add(this._codeIndexLucene.CreateStoreField(nameof(CodeFile.FileContent), m.FileContent));
                    this._codeFileLucene.AddDoc(codeFileDoc);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        private void Delete(CodeIndex m)
        {
            try
            {
                this._codeIndexLucene.DeleteDoc(m.Id, IndexField.Id);
                this._codeFileLucene.DeleteDoc(m.FileId);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        public void Enqueue(CodeIndex m)
        {
            this._docqueue.Enqueue(m);
        }

        public CodeIndex Get(string id)
        {
            try
            {
                var doc = this._codeIndexLucene.GetDoc(id, IndexField.Id);
                if (doc != null)
                {
                    var m = new CodeIndex
                    {
                        Id = doc.Get(IndexField.Id),
                        SearchText = doc.Get(IndexField.SearchText),
                        CodeBody = doc.Get(IndexField.CodeBody),
                        UserId = doc.Get(IndexField.UserId),
                        FileId = doc.Get(IndexField.FileId),
                    };

                    if (!m.FileId.IsNullOrEmpty())
                    {
                        var codeDoc = this._codeFileLucene.GetDoc(m.FileId);
                        if (codeDoc != null)
                        {
                            m.FileContent = codeDoc.Get(nameof(CodeFile.FileContent));
                        }
                    }

                    return m;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
                return null;
            }
        }

        public List<string> GetAutoCompleteList(string text, string language)
        {
            var searchResult = Search(text, language, 15);
            var result = searchResult.Select(t => t.SearchText.Left(100)).ToList();
            return result;
        }

        public static CodeIndexManager GetInstance(string indexPath)
        {
            if (_indexMgr == null)
            {
                if (indexPath.IsNullOrEmpty() == false)
                {
                    if (_indexMgr == null)
                    {
                        _indexMgr = new CodeIndexManager(indexPath);
                    }

                    return _indexMgr;
                }
                else
                {
                    throw new ArgumentNullException("indexPath");
                }
            }
            else
            {
                return _indexMgr;
            }
        }

        public List<CodeIndex> Search(string text, string language, int size = 300)
        {
            List<CodeIndex> result = new List<CodeIndex>();
            try
            {
                bool isExistIndex = this._codeIndexLucene.IndexExists();

                if (isExistIndex && !text.IsNullOrEmpty())
                {
                    IndexSearcher searcher = new IndexSearcher(this._codeIndexLucene.OpenReader());

                    BooleanQuery allQuery = new BooleanQuery();
                    allQuery.Add(new TermQuery(new Term(IndexField.Language, language)), BooleanClause.Occur.MUST);

                    //搜索条件
                    BooleanQuery shouldQuery = new BooleanQuery();

                    //把用户输入的关键字进行分词
                    foreach (string word in SplitContent.SplitWords(text))
                    {
                        PhraseQuery query1 = new PhraseQuery();
                        query1.Add(new Term(IndexField.SearchText, word));
                        shouldQuery.Add(query1, BooleanClause.Occur.SHOULD);
                    }

                    allQuery.Add(shouldQuery, BooleanClause.Occur.MUST);

                    MultiSearcher multiSearch = new MultiSearcher(new[] { searcher });
                    TopScoreDocCollector collector = TopScoreDocCollector.create(size, true);
                    multiSearch.Search(allQuery, collector);

                    ScoreDoc[] docs = collector.TopDocs(0, collector.GetTotalHits()).scoreDocs.OrderByDescending(t => t.score).ToArray();
                    for (int i = 0; i < docs.Length; i++)
                    {
                        int docId = docs[i].doc;//得到查询结果文档的id（Lucene内部分配的id）
                        Document doc = searcher.Doc(docId);//根据文档id来获得文档对象Document
                        var m = new CodeIndex
                        {
                            Id = doc.Get(IndexField.Id),
                            SearchText = doc.Get(IndexField.SearchText),
                            CodeBody = doc.Get(IndexField.CodeBody),
                            UserId = doc.Get(IndexField.UserId),
                            Language = doc.Get(IndexField.Language)
                        };

                        var codeDoc = this._codeFileLucene.GetDoc(m.FileId);
                        if (codeDoc != null)
                        {
                            m.FileContent = codeDoc.Get(nameof(CodeFile.FileContent));
                        }

                        result.Add(m);

                        // 当完全匹配时，只返回此条
                        if (text != null && m.SearchText != null && m.SearchText.Trim().ToLower() == text.Trim().ToLower()) break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }

            return result;
        }

        private void Start()
        {
            new TaskFactory().StartNew(() =>
            {
                while (true)
                {
                    while (this._docqueue.Count > 0)
                    {
                        var doc = this._docqueue.Dequeue();
                        switch (doc.Operate)
                        {
                            case Operate.Delete:
                                Delete(doc);
                                break;
                            case Operate.AddOrUpdate:
                                Create(doc);
                                break;
                        }
                    }

                    System.Threading.Thread.Sleep(2000);
                }
            });
        }

        #endregion Methods of CodeIndexManager (8)
    }
}
