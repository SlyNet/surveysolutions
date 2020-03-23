using System;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.SharedKernels.DataCollection.Aggregates;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;

namespace WB.Core.SharedKernels.Enumerator.ViewModels.InterviewDetails
{
    public class InstructionDynamicTextViewModel : BaseDynamicTextViewModel
    {
        public InstructionDynamicTextViewModel(IViewModelEventRegistry eventRegistry, IStatefulInterviewRepository interviewRepository, ISubstitutionService substitutionService, IQuestionnaireStorage questionnaireStorage, AnswerNotifier answerNotifier) : base(eventRegistry, interviewRepository, substitutionService, questionnaireStorage, answerNotifier)
        {
        }

        protected override string GetBrowserReadyTextHtml(IStatefulInterview interview)
        {
            return (interview.GetBrowserReadyInstructionsHtml(this.identity) ?? "").Replace(Environment.NewLine, "<br/>");
        }
    }
}
