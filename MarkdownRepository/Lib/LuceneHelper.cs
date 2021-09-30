using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis.PanGu;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;


namespace MarkdownRepository.Lib
{
    public class LuceneHelper<T>
        where T:IDoc
    {
        private string _storePath;
        private FSDirectory _fsDir = null;

        public LuceneHelper(string storePath)
        {
            this._storePath = Path.Combine(storePath, typeof(T).Name);
            if (!System.IO.Directory.Exists(_storePath))
                System.IO.Directory.CreateDirectory(_storePath);
            this._fsDir = FSDirectory.Open(new DirectoryInfo(this._storePath), 
                new NoLockFactory());
        }

        public IndexReader OpenReader()
        {
            if (!this.IndexExists()) return null;

            var indexReader = IndexReader.Open(this._fsDir, false);
            return indexReader;
        }

        public Field CreateStoreField(string fieldName, string value)
        {
            return new Field(fieldName, 
                value, 
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED, 
                Field.TermVector.WITH_POSITIONS_OFFSETS
                );
        }

        public Field CreateSearchField(string fieldName, string value)
        {
            return new Field(fieldName,
                value,
                Field.Store.YES,
                Field.Index.ANALYZED,
                Field.TermVector.WITH_POSITIONS_OFFSETS
                );
        }

        public bool IndexExists()
        {
            return IndexReader.IndexExists(this._fsDir);
        }

        public virtual bool ExistsDoc(string id, string fieldName = null)
        {
            var doc = GetDoc(id, fieldName);
            return doc != null;
        }

        public virtual Document GetDoc(string id, string fieldName=null)
        {
            if (id.IsNullOrEmpty()) return null;
            if (!this.IndexExists()) return null;

            var reader = OpenReader();
            var term = new Term(nameof(IDoc.Id), id);
            if (fieldName != null)
            {
                term = new Term(fieldName, id);
            }

            var termDocs = reader.TermDocs(term);
            if (termDocs.Next())
            {
                var docId = termDocs.Doc();
                var doc = reader.Document(docId);
                reader.Close();

                return doc;
            }

            return null;
        }

        public virtual void DeleteDoc(string id, string fieldName = null)
        {
            if (id.IsNullOrEmpty()) return;
            if (!this.IndexExists()) return;

            var reader = OpenReader();
            var term = new Term(nameof(IDoc.Id), id);
            if (fieldName != null)
            {
                term = new Term(fieldName, id);
            }

            reader.DeleteDocuments(term);
            reader.Commit();
            reader.Close();
        }

        public virtual void AddDoc(Document doc)
        {
            bool isExistIndex = IndexReader.IndexExists(this._fsDir);
            var writer = new IndexWriter(this._fsDir,
                new PanGuAnalyzer(),
                !isExistIndex,
                IndexWriter.MaxFieldLength.UNLIMITED);
            writer.AddDocument(doc);
            writer.Commit();
            writer.Optimize();
            writer.Close();
        }
    }

    public interface IDoc
    {
        string Id { get; set; }
    }
}