using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopOnContainers.Services.Locations.API.Infrastructure.Services;
using Microsoft.eShopOnContainers.Services.Locations.API.Model;
using Microsoft.eShopOnContainers.Services.Locations.API.ViewModel;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using zipkin4net;

namespace Locations.API.Controllers
{
    [Route("api/v1/[controller]")]
    [Authorize]
    public class LocationsController : ControllerBase
    {
        private readonly ILocationsService _locationsService;
        private readonly IIdentityService _identityService;
        private zipkin4net.Trace trace;
        public LocationsController()
        {
            trace = zipkin4net.Trace.Create();
        }

        public LocationsController(ILocationsService locationsService, IIdentityService identityService)
        {
            trace = zipkin4net.Trace.Create();
            _locationsService = locationsService ?? throw new ArgumentNullException(nameof(locationsService));
            _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        }

        //GET api/v1/[controller]/user/1
        [Route("user/{userId:guid}")]
        [HttpGet]
        [ProducesResponseType(typeof(UserLocation), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetUserLocation(Guid userId)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("LocationsController:GetUserLocation"));
            trace.Record(Annotations.Rpc("GET"));
            var userLocation = await _locationsService.GetUserLocation(userId.ToString());
            trace.Record(Annotations.ServerSend());
            return Ok(userLocation);
        }

        //GET api/v1/[controller]/
        [Route("")]
        [HttpGet]
        //[ProducesResponseType(typeof(List<Locations>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAllLocations()
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("LocationsController:GetUserLocation"));
            trace.Record(Annotations.Rpc("GET"));

            var locations = await _locationsService.GetAllLocation();
            trace.Record(Annotations.ServerSend());
            return Ok(locations);
        }

        //GET api/v1/[controller]/1
        [Route("{locationId}")]
        [HttpGet]
        //[ProducesResponseType(typeof(List<Locations>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLocation(int locationId)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("LocationsController:GetLocation"));
            trace.Record(Annotations.Rpc("GET"));

            var location = await _locationsService.GetLocation(locationId);
            trace.Record(Annotations.ServerSend());
            return Ok(location);
        }
         
        //POST api/v1/[controller]/
        [Route("")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateOrUpdateUserLocation([FromBody]LocationRequest newLocReq)
        {
            trace.Record(Annotations.ServerRecv());
            trace.Record(Annotations.ServiceName("LocationsController:GetLocation"));
            trace.Record(Annotations.Rpc("POST"));

            var userId = _identityService.GetUserIdentity();
            var result = await _locationsService.AddOrUpdateUserLocation(userId, newLocReq);
           
            trace.Record(Annotations.ServerSend());
            return result ? 
                (IActionResult)Ok() : 
                (IActionResult)BadRequest();
        }
    }
}
