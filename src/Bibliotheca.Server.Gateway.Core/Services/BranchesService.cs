using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bibliotheca.Server.Depository.Abstractions.DataTransferObjects;
using Bibliotheca.Server.Depository.Client;
using Bibliotheca.Server.Gateway.Core.DataTransferObjects;
using Bibliotheca.Server.Gateway.Core.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using YamlDotNet.Serialization;

namespace Bibliotheca.Server.Gateway.Core.Services
{
    public class BranchesService : IBranchesService
    {
        private readonly IBranchesClient _branchesClient;

        private readonly IMemoryCache _memoryCache;

        public BranchesService(IBranchesClient branchesClient, IMemoryCache memoryCache)
        {
            _branchesClient = branchesClient;
            _memoryCache = memoryCache;
        }

        public async Task<IList<ExtendedBranchDto>> GetBranchesAsync(string projectId)
        {
            List<ExtendedBranchDto> branches = null;
            string cacheKey = GetCacheKey(projectId);

            if(!_memoryCache.TryGetValue(cacheKey, out branches))
            {
                var branchesDto = await _branchesClient.Get(projectId);
                branches = branchesDto.Select(x => CreateExtendedBranchDto(x)).ToList();

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));

                _memoryCache.Set(cacheKey, branches, cacheEntryOptions);
            }
            
            return branches;
        }

        public async Task<ExtendedBranchDto> GetBranchAsync(string projectId, string branchName)
        {
            var branch = await _branchesClient.Get(projectId, branchName);
            return CreateExtendedBranchDto(branch);
        }

        public async Task CreateBranchAsync(string projectId, BranchDto branch)
        {
            var result = await _branchesClient.Post(projectId, branch);
            if(!result.IsSuccessStatusCode)
            {
                var content = await result.Content.ReadAsStringAsync();
                throw new CreateBranchException("During creatint the new branch error occurs: " + content);
            }

            ClearCache(projectId);
        }

        public async Task UpdateBranchAsync(string projectId, string branchName, BranchDto branch)
        {
            var result = await _branchesClient.Put(projectId, branchName, branch);
            if(!result.IsSuccessStatusCode)
            {
                var content = await result.Content.ReadAsStringAsync();
                throw new UpdateBranchException("During updating the branch error occurs: " + content);
            }

            ClearCache(projectId);
        }
        
        public async Task DeleteBranchAsync(string projectId, string branchName)
        {
            var result = await _branchesClient.Delete(projectId, branchName);
            if(!result.IsSuccessStatusCode)
            {
                var content = await result.Content.ReadAsStringAsync();
                throw new DeleteBranchException("During deleteing the branch error occurs: " + content);
            }

            ClearCache(projectId);
        }

        private ExtendedBranchDto CreateExtendedBranchDto(BranchDto branchDto)
        {
            var extendedBranchDto = new ExtendedBranchDto(branchDto);
            var mkDocsConfiguration = ReadMkDocsConfiguration(branchDto.MkDocsYaml);

            if (mkDocsConfiguration.ContainsKey("docs_dir"))
            {
                extendedBranchDto.DocsDir = mkDocsConfiguration["docs_dir"].ToString();
            }
            else
            {
                extendedBranchDto.DocsDir = "docs";
            }

            if (mkDocsConfiguration.ContainsKey("site_name"))
            {
                extendedBranchDto.SiteName = mkDocsConfiguration["site_name"].ToString();
            }

            return extendedBranchDto;
        }

        private Dictionary<object, object> ReadMkDocsConfiguration(string yamlFileContent)
        {
            using (var reader = new StringReader(yamlFileContent))
            {
                var deserializer = new Deserializer();

                var banchConfiguration = deserializer.Deserialize(reader) as Dictionary<object, object>;
                return banchConfiguration;
            }
        }

        private string GetCacheKey(string projectId) 
        {
            return $"BranchesService#{projectId}";
        }

        public void ClearCache(string projectId)
        {
            var cacheKey = GetCacheKey(projectId);
            _memoryCache.Remove(cacheKey);
        }
    }
}