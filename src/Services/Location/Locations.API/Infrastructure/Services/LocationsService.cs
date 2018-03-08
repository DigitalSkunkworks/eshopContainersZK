namespace Microsoft.eShopOnContainers.Services.Locations.API.Infrastructure.Services
{
    using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Abstractions;
    using Microsoft.eShopOnContainers.Services.Locations.API.Infrastructure.Exceptions;
    using Microsoft.eShopOnContainers.Services.Locations.API.Infrastructure.Repositories;
    using Microsoft.eShopOnContainers.Services.Locations.API.IntegrationEvents.Events;
    using Microsoft.eShopOnContainers.Services.Locations.API.Model;
    using Microsoft.eShopOnContainers.Services.Locations.API.ViewModel;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using zipkin4net;

    public class LocationsService : ILocationsService
    {
        private readonly ILocationsRepository _locationsRepository;
        private readonly IEventBus _eventBus;
        private zipkin4net.Trace trace;
        public LocationsService()
        {
            trace = zipkin4net.Trace.Create();
        }

        public LocationsService(ILocationsRepository locationsRepository, IEventBus eventBus)
        {
            trace = zipkin4net.Trace.Create();
            _locationsRepository = locationsRepository ?? throw new ArgumentNullException(nameof(locationsRepository));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public async Task<Locations> GetLocation(int locationId)
        {
            return await _locationsRepository.GetAsync(locationId);
        }

        public async Task<UserLocation> GetUserLocation(string userId)
        {
            return await _locationsRepository.GetUserLocationAsync(userId);
        }

        public async Task<List<Locations>> GetAllLocation()
        {
            return await _locationsRepository.GetLocationListAsync();
        }

        public async Task<bool> AddOrUpdateUserLocation(string userId, LocationRequest currentPosition)
        {            
            trace.Record(Annotations.LocalOperationStart("LocationsService:AddOrUpdateUserLocation"));
            // Get the list of ordered regions the user currently is within
            var currentUserAreaLocationList = await _locationsRepository.GetCurrentUserRegionsListAsync(currentPosition);
                      
            if(currentUserAreaLocationList is null)
            {
                trace.Record(Annotations.LocalOperationStop());
                throw new LocationDomainException("User current area not found");
            }

            // If current area found, then update user location
            var locationAncestors = new List<string>();
            var userLocation = await _locationsRepository.GetUserLocationAsync(userId);
            userLocation = userLocation ?? new UserLocation();
            userLocation.UserId = userId;
            userLocation.LocationId = currentUserAreaLocationList[0].LocationId;
            userLocation.UpdateDate = DateTime.UtcNow;
            await _locationsRepository.UpdateUserLocationAsync(userLocation);

            // Publish integration event to update marketing read data model
            // with the new locations updated
            PublishNewUserLocationPositionIntegrationEvent(userId, currentUserAreaLocationList);

            trace.Record(Annotations.LocalOperationStop());
            return true;
        }

        private void PublishNewUserLocationPositionIntegrationEvent(string userId, List<Locations> newLocations)
        {
            trace.Record(Annotations.LocalOperationStart("LocationsService:PublishNewUserLocationPositionIntegrationEvent"));
            var newUserLocations = MapUserLocationDetails(newLocations);
            var @event = new UserLocationUpdatedIntegrationEvent(userId, newUserLocations);
            _eventBus.Publish(@event);
            trace.Record(Annotations.LocalOperationStop());
        }

        private List<UserLocationDetails> MapUserLocationDetails(List<Locations> newLocations)
        {
            trace.Record(Annotations.LocalOperationStart("LocationsService:MapUserLocationDetails"));
            var result = new List<UserLocationDetails>();
            newLocations.ForEach(location => {
                result.Add(new UserLocationDetails()
                {
                    LocationId = location.LocationId,
                    Code = location.Code,
                    Description = location.Description
                });
            });

            trace.Record(Annotations.LocalOperationStop());
            return result;
        }
    }
}
