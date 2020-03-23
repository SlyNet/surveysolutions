using System;
using System.Linq;
using System.Text.RegularExpressions;
using MvvmCross.ViewModels;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Aggregates;
using WB.Core.SharedKernels.DataCollection.Events.Interview;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.DataCollection.Utils;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;
using WB.Core.SharedKernels.SurveySolutions.Documents;

namespace WB.Core.SharedKernels.Enumerator.ViewModels.InterviewDetails
{
    public abstract class BaseDynamicTextViewModel : MvxNotifyPropertyChanged,
        IViewModelEventHandler<SubstitutionTitlesChanged>,
        IViewModelEventHandler<RosterInstancesTitleChanged>,
        IDisposable
    {
        private static readonly Regex HtmlRemovalRegex = new Regex(Constants.HtmlRemovalPattern);

        protected readonly IViewModelEventRegistry eventRegistry;
        protected readonly IStatefulInterviewRepository interviewRepository;
        protected readonly ISubstitutionService substitutionService;
        protected readonly IQuestionnaireStorage questionnaireStorage;
        protected readonly AnswerNotifier answerNotifier;

        public BaseDynamicTextViewModel(
            IViewModelEventRegistry eventRegistry,
            IStatefulInterviewRepository interviewRepository,
            ISubstitutionService substitutionService,
            IQuestionnaireStorage questionnaireStorage,
            AnswerNotifier answerNotifier)
        {
            this.eventRegistry = eventRegistry;
            this.interviewRepository = interviewRepository;
            this.substitutionService = substitutionService;
            this.questionnaireStorage = questionnaireStorage;
            this.answerNotifier = answerNotifier;
        }

        private string interviewId;
        protected Identity identity;

        public void InitAsStatic(string textWithoutSubstitutions, Identity entityId = null)
        {
            this.HtmlText = textWithoutSubstitutions ?? throw new ArgumentNullException(nameof(textWithoutSubstitutions));
            this.PlainText = textWithoutSubstitutions;
            this.identity = entityId;
        }

        public virtual void Init(string interviewId, Identity entityIdentity)
        {
            if (interviewId == null) throw new ArgumentNullException(nameof(interviewId));
            if (entityIdentity == null) throw new ArgumentNullException(nameof(entityIdentity));

            this.interviewId = interviewId;
            this.identity = entityIdentity;

            this.UpdateText();

            this.eventRegistry.Subscribe(this, interviewId);
        }

        private string htmlText;
        public string HtmlText
        {
            get => this.htmlText;
            set => this.RaiseAndSetIfChanged(ref this.htmlText, value);
        }

        private string plainText;
        public string PlainText
        {
            get => this.plainText;
            set => this.RaiseAndSetIfChanged(ref this.plainText, value);
        }

        public void Dispose()
        {
            this.eventRegistry.Unsubscribe(this);
        }

        public virtual void Handle(SubstitutionTitlesChanged @event)
        {
            bool shouldUpdateTexts =
                @event.Questions.Contains(this.identity) ||
                @event.StaticTexts.Contains(this.identity) ||
                @event.Groups.Contains(this.identity);

            if (!shouldUpdateTexts) return;

            this.UpdateText();
        }

        public virtual void Handle(RosterInstancesTitleChanged @event)
        {
            if (!@event.ChangedInstances.Any(x => x.RosterInstance.GetIdentity().Equals(this.identity)))
                return;

            this.UpdateText();
        }

        private void UpdateText()
        {
            var interview = this.interviewRepository.Get(this.interviewId);
            this.HtmlText = GetBrowserReadyTextHtml(interview);
            this.PlainText = RemoveHtmlTags(this.HtmlText);
        }

        protected abstract string GetBrowserReadyTextHtml(IStatefulInterview interview);

        private static string RemoveHtmlTags(string rawText) => HtmlRemovalRegex.Replace(rawText, string.Empty);
    }
}
