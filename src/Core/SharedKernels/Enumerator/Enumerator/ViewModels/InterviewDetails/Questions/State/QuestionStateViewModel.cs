﻿using System;
using MvvmCross.Commands;
using MvvmCross.ViewModels;
using WB.Core.Infrastructure.EventBus.Lite;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Events.Interview.Base;
using WB.Core.SharedKernels.DataCollection.Repositories;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;

namespace WB.Core.SharedKernels.Enumerator.ViewModels.InterviewDetails.Questions.State
{
    public class QuestionStateViewModel<TAnswerEvent>: MvxNotifyPropertyChanged,
        IViewModelEventHandler<TAnswerEvent>,
        IQuestionStateViewModel,
        IDisposable
        where TAnswerEvent : QuestionAnswered
    {
        public QuestionHeaderViewModel Header { get; private set; }
        public virtual ValidityViewModel Validity { get; private set; }
        public virtual WarningsViewModel Warnings { get; }
        public EnablementViewModel Enablement { get; private set; }
        public CommentsViewModel Comments { get; private set; }

        private readonly IViewModelEventRegistry liteEventRegistry;
        private readonly IStatefulInterviewRepository interviewRepository;
        private readonly AnswersRemovedNotifier answersRemovedNotifier;
        private Identity questionIdentity;
        private string interviewId;

        protected QuestionStateViewModel() { }

        public QuestionStateViewModel(
            IViewModelEventRegistry liteEventRegistry,
            IStatefulInterviewRepository interviewRepository,
            ValidityViewModel validityViewModel, 
            QuestionHeaderViewModel questionHeaderViewModel,
            EnablementViewModel enablementViewModel,
            CommentsViewModel commentsViewModel,
            AnswersRemovedNotifier answersRemovedNotifier,
            WarningsViewModel warningsViewModel)
        {
            this.liteEventRegistry = liteEventRegistry;
            this.interviewRepository = interviewRepository;
            this.answersRemovedNotifier = answersRemovedNotifier;
            this.Warnings = warningsViewModel;
            this.Validity = validityViewModel;
            this.Header = questionHeaderViewModel;
            this.Enablement = enablementViewModel;
            this.Comments = commentsViewModel;
        }
        
        public virtual void Init(string interviewId, Identity entityIdentity, NavigationState navigationState)
        {
            this.questionIdentity = entityIdentity ?? throw new ArgumentNullException(nameof(entityIdentity));
            this.interviewId = interviewId ?? throw new ArgumentNullException(nameof(interviewId));
            
            var interview = this.interviewRepository.Get(interviewId);
            this.IsAnswered = interview.WasAnswered(entityIdentity);

            this.answersRemovedNotifier.Init(interviewId, entityIdentity);
            
            this.Validity.Init(interviewId, entityIdentity, navigationState);
            this.Warnings.Init(interviewId, entityIdentity, navigationState);
            this.Comments.Init(interviewId, entityIdentity, navigationState);
            this.Enablement.Init(interviewId, entityIdentity);
            this.Enablement.EntityEnabled += this.EnablementOnEntityEnabled;
            this.Header.Init(interviewId, entityIdentity, this.Enablement, navigationState);

            this.answersRemovedNotifier.AnswerRemoved += this.AnswerRemoved;
            this.Header.ShowComments += this.ShowCommentsCommand;

            this.liteEventRegistry.Subscribe(this, interviewId);
        }

        private void AnswerRemoved(object sender, EventArgs eventArgs)
        {
            this.UpdateFromModel();
        }

        private void EnablementOnEntityEnabled(object sender, EventArgs eventArgs)
        {
            this.UpdateFromModel();
        }

        private bool isAnswered;
        public bool IsAnswered
        {
            get => this.isAnswered;
            set => SetProperty(ref this.isAnswered, value);
        }

        public void Handle(TAnswerEvent @event)
        {
            if (this.questionIdentity.Equals(@event.QuestionId, @event.RosterVector))
            {
                this.UpdateFromModel();
            }
        }

        private void UpdateFromModel()
        {
            var interview = this.interviewRepository.Get(this.interviewId);
            this.IsAnswered = interview.WasAnswered(this.questionIdentity);
        }

        public IMvxCommand ShowCommentEditorCommand =>  new MvxCommand(() => this.ShowCommentsCommand(this, EventArgs.Empty));

        private void ShowCommentsCommand(object sender, EventArgs eventArgs)
        {
            if (this.Enablement.Enabled)
            {
                this.Comments.ShowCommentInEditor();
            }
        }

        public void Dispose()
        {
            this.liteEventRegistry.Unsubscribe(this);
            this.Enablement.EntityEnabled -= this.EnablementOnEntityEnabled;
            this.answersRemovedNotifier.AnswerRemoved -= this.AnswerRemoved;
            this.Header.ShowComments -= this.ShowCommentsCommand;
            Header.Dispose();
            Validity.Dispose();
            Warnings.Dispose();
            Enablement.Dispose();
            this.answersRemovedNotifier.Dispose();
        }
    }
}
