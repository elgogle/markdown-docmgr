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

namespace MarkdownRepository.Lib
{
    public class CodeIndexManager
    {
        private static object _lock = new object();
        private static CodeIndexManager _indexMgr = null;
        private FSDirectory _fsDir = null;
        private IndexReader _indexReader = null;
        private IndexWriter _indexWriter = null;
        private Queue<CodeModel> _docqueue = new Queue<CodeModel>();

        public static CodeIndexManager GetInstance(string indexPath)
        {
            if(_indexMgr == null)
            {
                if(indexPath.IsNullOrEmpty() == false)
                {
                    if (_indexMgr == null)
                        _indexMgr = new CodeIndexManager(indexPath);

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

        public void Enqueue(CodeModel m)
        {
            this._docqueue.Enqueue(m);
        }

        public CodeModel Get(string id)
        {
            try
            {
                this._indexReader = IndexReader.Open(this._fsDir, false);
                var termDocs = this._indexReader.TermDocs(new Term(IndexField.Id, id));
                termDocs.Next();
                var docId = termDocs.Doc();
                var doc = this._indexReader.Document(docId);
                var m = new CodeModel
                {
                    Id = doc.Get(IndexField.Id),
                    SearchText = doc.Get(IndexField.SearchText),
                    CodeBody = doc.Get(IndexField.CodeBody),
                    UserId = doc.Get(IndexField.UserId)
                };

                this._indexReader.Close();
                return m;
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
                return null;
            }
        }

        public List<CodeModel> Search(string text, string language)
        {
            List<CodeModel> result = new List<CodeModel>();
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);
                if (isExistIndex)
                {
                    this._indexReader = IndexReader.Open(this._fsDir, false);
                    IndexSearcher searcher = new IndexSearcher(this._indexReader);

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
                    TopScoreDocCollector collector = TopScoreDocCollector.create(300, true);
                    multiSearch.Search(allQuery, collector);

                    ScoreDoc[] docs = collector.TopDocs(0, collector.GetTotalHits()).scoreDocs.OrderByDescending(t => t.score).ToArray();
                    for (int i = 0; i < docs.Length; i++)
                    {
                        int docId = docs[i].doc;//得到查询结果文档的id（Lucene内部分配的id）
                        Document doc = searcher.Doc(docId);//根据文档id来获得文档对象Document
                        var m = new CodeModel
                        {
                            Id = doc.Get(IndexField.Id),
                            SearchText = doc.Get(IndexField.SearchText),
                            CodeBody = doc.Get(IndexField.CodeBody),
                            UserId = doc.Get(IndexField.UserId),
                            Language = doc.Get(IndexField.Language)
                        };
                        result.Add(m);
                    }
                }
            }
            catch(Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }

            return result;
        }

        public List<string> GetAutoCompleteList(string text, string language)
        {
            var searchResult = Search(text, language).Take(15);
            var result = searchResult.Select(t => t.SearchText.Left(100)).ToList();
            return result;
        }

        private CodeIndexManager(string indexPath)
        {
            if (!System.IO.Directory.Exists(indexPath))
                System.IO.Directory.CreateDirectory(indexPath);

            this._fsDir = FSDirectory.Open(new DirectoryInfo(indexPath), new NoLockFactory());
            this.Start();
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
                        switch(doc.Operate)
                        {
                            case Operate.Delete:
                                Delete(doc.Id);
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

        private void Create(CodeModel m)
        {
            try
            {
                bool isExistIndex = IndexReader.IndexExists(this._fsDir);
                if (isExistIndex)
                {
                    Delete(m.Id);
                }

                Document doc = new Document();
                doc.Add(new Field(IndexField.Id, m.Id, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
                doc.Add(new Field(IndexField.SearchText, m.SearchText, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
                doc.Add(new Field(IndexField.Language, m.Language, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
                doc.Add(new Field(IndexField.CodeBody, m.CodeBody, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
                doc.Add(new Field(IndexField.UserId, m.UserId, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));

                this._indexWriter = new IndexWriter(this._fsDir, new PanGuAnalyzer(), !isExistIndex, IndexWriter.MaxFieldLength.UNLIMITED);
                this._indexWriter.AddDocument(doc);
                this._indexWriter.Commit();
                this._indexWriter.Optimize();
                this._indexWriter.Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        private void Delete(string id)
        {
            try
            {
                this._indexReader = IndexReader.Open(this._fsDir, false);
                this._indexReader.DeleteDocuments(new Term(IndexField.Id, id));
                this._indexReader.Commit();
                this._indexReader.Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteError(this.GetType(), ex);
            }
        }

        struct IndexField
        {
            public const string Id = "ID";
            public const string SearchText = "SearchText";
            public const string CodeBody = "CodeBody";
            public const string UserId = "UserId";
            public const string Language = "Language";
        }
    }

    public class CodeModel
    {
        public string Id { get; set; }
        public string SearchText { get; set; }
        public string CodeBody { get; set; }
        public Operate Operate { get; set; }
        public string UserId { get; set; }
        public string Language { get; set; }
    }
}