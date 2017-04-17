﻿using System.Threading.Tasks;
using Bibliotheca.Server.Gateway.Core.DataTransferObjects;
using Bibliotheca.Server.Gateway.Core.Policies;
using Bibliotheca.Server.Gateway.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bibliotheca.Server.Gateway.Api.Controllers
{
    /// <summary>
    /// Controller which manages projects infrmation.
    /// </summary>
    [Authorize]
    [ApiVersion("1.0")]
    [Route("api/projects")]
    public class ProjectsController : Controller
    {
        private readonly IProjectsService _projectsService;

        private readonly IAuthorizationService _authorizationService;

        private readonly IUsersService _usersService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="projectsService">Project service.</param>
        /// <param name="authorizationService">Authorization service.</param>
        /// <param name="usersService">Users service.</param>
        public ProjectsController(
            IProjectsService projectsService, 
            IAuthorizationService authorizationService, 
            IUsersService usersService)
        {
            _projectsService = projectsService;
            _authorizationService = authorizationService;
            _usersService = usersService;
        }

        /// <summary>
        /// Get list of projects.
        /// </summary>
        /// <remarks>
        /// Endpoint returns projects stored in the system.
        /// </remarks>
        /// <param name="filter">Filter which can be used to refine the results.</param>
        /// <returns>List of projects.</returns>
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(FilteredResutsDto<ProjectDto>))]
        public async Task<FilteredResutsDto<ProjectDto>> Get([FromQuery] ProjectsFilterDto filter)
        {
            var userId = User.Identity.Name.ToLower();
            var projects = await _projectsService.GetProjectsAsync(filter, userId);
            return projects;
        }

        /// <summary>
        /// Get specific project.
        /// </summary>
        /// <remarks>
        /// Endpoint returns detailed information about specific project.
        /// </remarks>
        /// <param name="projectId">Project id.</param>
        /// <returns>Information about specific project.</returns>
        [HttpGet("{projectId}")]
        [ProducesResponseType(200, Type = typeof(ProjectDto))]
        public async Task<IActionResult> Get(string projectId)
        {
            var projectFromStorage = await _projectsService.GetProjectAsync(projectId);
            if (projectFromStorage == null)
            {
                return NotFound();
            }

            var isAuthorize = await _authorizationService.AuthorizeAsync(User, projectFromStorage, Operations.Read);
            if (!isAuthorize)
            {
                return Forbid();
            }

            return new ObjectResult(projectFromStorage);
        }

        /// <summary>
        /// Create a new project.
        /// </summary>
        /// <remarks>
        /// Endpoint for creating a new project. Project is automatically assigned to the user (if authorization service is enabled).
        /// </remarks>
        /// <param name="project">Project information.</param>
        /// <returns>If created successfully endpoint returns 201 (Created).</returns>
        [HttpPost]
        [Authorize("CanAddProject")]
        [ProducesResponseType(201)]
        public async Task<IActionResult> Post([FromBody] ProjectDto project)
        {
            await _projectsService.CreateProjectAsync(project);
            await _usersService.AddProjectToUserAsync(User.Identity.Name, project.Id);
            return Created($"/projects/{project.Id}", project);
        }

        /// <summary>
        /// Update information about project.
        /// </summary>
        /// <remarks>
        /// Endpoint for updating information about project.
        /// </remarks>
        /// <param name="projectId">Project id.</param>
        /// <param name="project">Project information.</param>
        /// <returns>If updated successfully endpoint returns 200 (Ok).</returns>
        [HttpPut("{projectId}")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Put(string projectId, [FromBody] ProjectDto project)
        {
            var projectFromStorage = await _projectsService.GetProjectAsync(projectId);
            if (projectFromStorage == null)
            {
                return NotFound();
            }

            var isAuthorize = await _authorizationService.AuthorizeAsync(User, projectFromStorage, Operations.Update);
            if (!isAuthorize)
            {
                return Forbid();
            }

            await _projectsService.UpdateProjectAsync(projectId, project);
            return Ok();
        }

        /// <summary>
        /// Get project access token.
        /// </summary>
        /// <remarks>
        /// Endpoint for getting project token assigned to the project. That project token can be used to sending documentation files
        /// to all branches in project. Project token can be downloaded only by users assigned to specific project (if authorization 
        /// service is enabled).
        /// </remarks>
        /// <param name="projectId">Project id.</param>
        /// <returns>Project token for specific project.</returns>
        [HttpGet("{projectId}/accessToken")]
        [ProducesResponseType(200, Type = typeof(AccessTokenDto))]
        public async Task<IActionResult> GetProjectAccessToken(string projectId)
        {
            var projectFromStorage = await _projectsService.GetProjectAsync(projectId);
            if (projectFromStorage == null)
            {
                return NotFound();
            }

            var isAuthorize = await _authorizationService.AuthorizeAsync(User, projectFromStorage, Operations.Update);
            if (!isAuthorize)
            {
                return Forbid();
            }

            AccessTokenDto accessToken = await _projectsService.GetProjectAccessTokenAsync(projectId);
            return new ObjectResult(accessToken);
        }

        /// <summary>
        /// Delete specific project.
        /// </summary>
        /// <remarks>
        /// Endpoint for deleting specific project. Besides project information all branches and documentation files are deleting. 
        /// Also documentation is deleted from search index (if search service is enabled). 
        /// </remarks>
        /// <param name="projectId">Project id.</param>
        /// <returns>If deleted successfully endpoint returns 200 (Ok).</returns>
        [HttpDelete("{projectId}")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Delete(string projectId)
        {
            var projectFromStorage = await _projectsService.GetProjectAsync(projectId);
            if (projectFromStorage == null)
            {
                return NotFound();
            }

            var isAuthorize = await _authorizationService.AuthorizeAsync(User, projectFromStorage, Operations.Delete);
            if (!isAuthorize)
            {
                return Forbid();
            }

            // TODO: Project should be removed also from search index.
            await _projectsService.DeleteProjectAsync(projectId);
            return Ok();
        }
    }
}
