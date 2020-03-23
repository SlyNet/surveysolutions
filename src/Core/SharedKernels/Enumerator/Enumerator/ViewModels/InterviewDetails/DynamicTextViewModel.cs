using System;
using System.Linq;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Aggregates;
using WB.Core.SharedKernels.DataCollection.Events.Interview;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;

namespace WB.Core.SharedKernels.Enumerator.ViewModels.InterviewDetails
{
    public class DynamicTextViewModel : BaseDynamicTextViewModel
    {
        public DynamicTextViewModel(IViewModelEventRegistry eventRegistry, IStatefulInterviewRepository interviewRepository, ISubstitutionService substitutionService, IQuestionnaireStorage questionnaireStorage, AnswerNotifier answerNotifier) : base(eventRegistry, interviewRepository, substitutionService, questionnaireStorage, answerNotifier)
        {
        }

        private bool shouldAppendRosterTitle;

        public override void Init(string interviewId, Identity entityIdentity)
        {
            var interview = this.interviewRepository.Get(interviewId);
            var questionnaire = this.questionnaireStorage.GetQuestionnaire(interview.QuestionnaireIdentity, interview.Language);

            this.shouldAppendRosterTitle = questionnaire.IsRosterGroup(this.identity.Id) &&
                                           !questionnaire.HasCustomRosterTitle(this.identity.Id);

            base.Init(interviewId, entityIdentity);
        }


        protected override string GetBrowserReadyTextHtml(IStatefulInterview interview)
        {
            var titleText = interview.GetBrowserReadyTitleHtml(this.identity) ?? "";

            return this.shouldAppendRosterTitle
                ? $"{titleText} - {interview.GetRosterTitle(this.identity) ?? this.substitutionService.DefaultSubstitutionText}"
                : titleText;
        }

        public override void Handle(RosterInstancesTitleChanged @event)
        {
            if (!this.shouldAppendRosterTitle) return;
            base.Handle(@event);
        }
    }
}
