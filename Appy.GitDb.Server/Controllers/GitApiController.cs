using System;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Appy.GitDb.Core.Interfaces;
using Appy.GitDb.Core.Model;
using Microsoft.AspNetCore.Http.Extensions;

namespace Appy.GitDb.Server
{
    public class GitApiController : ControllerBase
    {
        readonly IGitDb _gitDb;

        public GitApiController(IGitDb gitDb)
        {
            _gitDb = gitDb;
        }

        [Route("{branch}/document/{*key}")]
        [HttpGet]

        public Task<IActionResult> Get(string branch, string key) =>
            result(() => _gitDb.Get(branch, WebUtility.UrlDecode(key)));

        [Route("{branch}/documents/{*key}")]
        [HttpGet]
        public Task<IActionResult> GetFiles(string branch, string key) =>
            result(() => _gitDb.GetFiles(branch, key));

        [Route("{branch}/subfolders/{*key}")]
        [HttpGet]
        public Task<IActionResult> GetSubFolders(string branch, string key) =>
            result(() => _gitDb.GetSubfolders(branch, key));

        [Route("{branch}/document")]
        [HttpPost]
        public Task<IActionResult> Save(string branch, [FromBody] SaveRequest request) =>
            result(() => _gitDb.Save(branch, request.Message, request.Document, request.Author));

        [Route("{branch}/document/delete")]
        [HttpPost]
        public Task<IActionResult> Delete(string branch, [FromBody] DeleteRequest request) =>
            result(() => _gitDb.Delete(branch, request.Key, request.Message, request.Author));

        [Route("{branch}/transactions/close")]
        [HttpPost]
        public Task<IActionResult> CloseTransactions(string branch) =>
            result(() => _gitDb.CloseTransactions(branch));

        [Route("tag")]
        [HttpPost]
        public Task<IActionResult> Tag([FromBody] Reference reference) =>
            result(() => _gitDb.Tag(reference));

        [Route("branch")]
        [HttpGet]
        public Task<IActionResult> GetBranches() =>
            result(() => _gitDb.GetAllBranches());

        [Route("branch")]
        [HttpPost]
        public Task<IActionResult> CreateBranch([FromBody] Reference reference) =>
            result(() => _gitDb.CreateBranch(reference));
        static readonly Dictionary<string, ITransaction> _transactions = new Dictionary<string, ITransaction>();

        [Route("{branch}/transaction")]
        [HttpPost]
        public Task<IActionResult> CreateTransaction(string branch) =>
            result(async () =>
            {
                var trans = await _gitDb.CreateTransaction(branch);
                var transactionId = Guid.NewGuid().ToString();
                _transactions.Add(transactionId, trans);
                return transactionId;
            });

        [Route("{branch}")]
        [HttpDelete]
        public Task<IActionResult> DeleteBranch(string branch) =>
           result(() => _gitDb.DeleteBranch(branch));

        [Route("tag/{tag}")]
        [HttpDelete]
        public Task<IActionResult> DeleteTag(string tag) =>
            result(() => _gitDb.DeleteTag(tag));

        [Route("{transactionId}/add")]
        [HttpPost]
        public Task<IActionResult> AddToTransaction(string transactionId, Document document) =>
            result(() => _transactions[transactionId].Add(document));

        [Route("{transactionId}/addmany")]
        [HttpPost]
        public Task<IActionResult> AddToTransaction(string transactionId, List<Document> documents) =>
            result(() => _transactions[transactionId].AddMany(documents));


        [Route("{transactionId}/delete/{key}")]
        [HttpPost]
        public Task<IActionResult> DeleteInTransaction(string transactionId, string key) =>
            result(() => _transactions[transactionId].Delete(key));

        [Route("{transactionId}/deleteMany")]
        [HttpPost]
        public Task<IActionResult> DeleteInTransaction(string transactionId, List<string> keys) =>
            result(() => _transactions[transactionId].DeleteMany(keys));


        [Route("{transactionId}/commit")]
        [HttpPost]
        public Task<IActionResult> CommitTransaction(string transactionId, [FromBody] CommitTransaction commit) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                var sha = await transaction.Commit(commit.Message, commit.Author);
                _transactions.Remove(transactionId);
                return sha;
            });

        [Route("{transactionId}/abort")]
        [HttpPost]
        public Task<IActionResult> AbortTransaction(string transactionId) =>
            result(async () =>
            {
                var transaction = _transactions[transactionId];
                await transaction.Abort();
                _transactions.Remove(transactionId);
            });

        [Route("merge")]
        [HttpPost]
        public Task<IActionResult> Merge(MergeRequest mergeRequest) =>
           result(() =>_gitDb.MergeBranch(mergeRequest.Source, mergeRequest.Target, mergeRequest.Author, mergeRequest.Message));

        [Route("{branch}/rebase")]
        [HttpPost]
        public Task<IActionResult> Rebase(RebaseRequest rebaseRequest) =>
            result(() => _gitDb.RebaseBranch(rebaseRequest.Source, rebaseRequest.Target, rebaseRequest.Author, rebaseRequest.Message));

        [Route("diff/{reference}/{reference2}")]
        [HttpGet]
        public Task<IActionResult> Diff(string reference, string reference2) =>
            result(() => _gitDb.Diff(reference, reference2));

        [Route("log/{reference}/{reference2}")]
        [HttpGet]
        public Task<IActionResult> Log(string reference, string reference2) =>
            result(() => _gitDb.Log(reference, reference2));


        async Task<IActionResult> result<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (ArgumentException ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        async Task<IActionResult> result(Func<Task> action)
        {
            try
            {
                await action();
                return Ok();
            }
            catch (ArgumentException ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

    }
}