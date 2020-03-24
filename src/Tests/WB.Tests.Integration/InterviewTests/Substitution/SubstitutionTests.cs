using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using Ncqrs.Eventing.ServiceModel.Bus;
using Ncqrs.Eventing.Storage;
using Ncqrs.Spec;
using NUnit.Framework;
using WB.Core.Infrastructure.Aggregates;
using WB.Core.Infrastructure.CommandBus;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.DataCollection.Commands.Interview;
using WB.Core.SharedKernels.DataCollection.Events.Interview;
using WB.Core.SharedKernels.DataCollection.Implementation.Aggregates;
using WB.Core.SharedKernels.Questionnaire.Documents;
using WB.Core.SharedKernels.QuestionnaireEntities;
using WB.Tests.Abc;
using WB.Tests.Integration.CommandServiceTests;

namespace WB.Tests.Integration.InterviewTests.Substitution
{
    [TestFixture]
    public class SubstitutionTests : InterviewTestsContext
    {
        private AppDomainContext appDomainContext;

        [SetUp]
        public void SetupTest()
        {
            appDomainContext = AppDomainContext.Create();
        }

        [TearDown]
        public void TearDown()
        {
            appDomainContext.Dispose();
        }


        [Test]
        public void when_substitution_changed_should_fire_event_and_dont_save_it_in_event_store()
        {
            var results = Execute.InStandaloneAppDomain(appDomainContext.Domain, () =>
            {
                SetUp.MockedServiceLocator();

                var questionnaireDocument = Create.Entity.QuestionnaireDocumentWithOneChapter(
                    Create.Entity.TextQuestion(Id.g1, variable: "text"),
                    Create.Entity.NumericIntegerQuestion(Id.g2, variable: "num", questionText: "%text% correct?")
                );

                var interview = SetupStatefullInterview(appDomainContext.AssemblyLoadContext, questionnaireDocument);

                CommandRegistry
                    .Setup<StatefulInterview>()
                    .InitializesWith<CreateInterview>(command => command.InterviewId, (command, aggregate) => aggregate.CreateInterview(command))
                    .Handles<AnswerTextQuestionCommand>(command => command.InterviewId, (command, aggregate) => aggregate.AnswerTextQuestion(command.UserId, command.QuestionId, command.RosterVector, command.OriginDate, command.Answer));

                var repository = Mock.Of<IEventSourcedAggregateRootRepository>(_
                    => _.GetLatest(typeof(StatefulInterview), interview.Id) == interview);

                var eventStoreMock = new Mock<IEventStore>();
                var eventBusMock = new Mock<IEventBus>();

                var commandService = Create.Service.CommandService(repository: repository, eventBus: eventBusMock.Object,
                    eventStore: eventStoreMock.Object);

                // Act
                commandService.Execute(new .DoNothing(), null);


                using var eventContext = new EventContext();
                interview.AnswerTextQuestion(Guid.NewGuid(), Id.g1, RosterVector.Empty, DateTimeOffset.Now, "same text");

                var substitutionTitlesChangedEvent = eventContext.GetSingleEvent<SubstitutionTitlesChanged>();
                return new
                {
                    HasSubstitutionTitlesChangedEvent = substitutionTitlesChangedEvent != null,
                    EventStoreDontHaveSubstitutionTitlesChangedEvent = true
                };
            });

            Assert.That(results.HasSubstitutionTitlesChangedEvent, Is.True);
            Assert.That(results.EventStoreDontHaveSubstitutionTitlesChangedEvent, Is.True);
        }
    }
}
