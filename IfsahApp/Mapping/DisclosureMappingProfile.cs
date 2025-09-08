using AutoMapper;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;

namespace IfsahApp.Core.Mapping
{
    public class DisclosureMappingProfile : Profile
    {
        public DisclosureMappingProfile()
        {
            // Model -> ViewModel
            CreateMap<Disclosure, DisclosureFormViewModel>()
            .ForMember(dest => dest.Attachments, opt => opt.Ignore())
            .ForMember(dest => dest.SuspectedPersons, opt => opt.MapFrom(src => src.SuspectedPeople))
            .ForMember(dest => dest.RelatedPersons, opt => opt.MapFrom(src => src.RelatedPeople));

        // ViewModel -> Model
        CreateMap<DisclosureFormViewModel, Disclosure>()
            .ForMember(dest => dest.Attachments, opt => opt.Ignore())
            .ForMember(dest => dest.SuspectedPeople, opt => opt.MapFrom(src => src.SuspectedPersons))
            .ForMember(dest => dest.RelatedPeople, opt => opt.MapFrom(src => src.RelatedPersons))
            .ForMember(dest => dest.SubmittedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.FinalReview, opt => opt.Ignore())
            .ForMember(dest => dest.Comments, opt => opt.Ignore())
            .ForMember(dest => dest.Assignments, opt => opt.Ignore());
        }
    }
}
