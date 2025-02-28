﻿using System;
using Main.Core.Entities.Composite;
using Main.Core.Entities.SubEntities;
using NUnit.Framework;
using WB.Core.BoundedContexts.Designer.Aggregates;
using WB.Tests.Abc;

namespace WB.Tests.Unit.Designer.BoundedContexts.QuestionnaireTests
{
    [TestFixture]
    internal class DeleteGroupTests : QuestionnaireTestsContext
    {
        [SetUp]
        public void SetUp()
        {
            
        }

        [Test]
        public void DeleteGroup_When_group_public_key_specified_Then_raised_GroupDeleted_event_with_same_group_public_key()
        {
            // arrange
            Guid groupPublicKey = Guid.NewGuid();
            Guid responsibleId = Guid.NewGuid();
            Questionnaire questionnaire = CreateQuestionnaireWithOneGroup(groupId: groupPublicKey, responsibleId: responsibleId);

            // act
            Guid parentPublicKey = Guid.NewGuid();
            questionnaire.DeleteGroup(groupPublicKey, responsibleId: responsibleId);

            // assert
            Assert.That(questionnaire.QuestionnaireDocument.Find<IGroup>(groupPublicKey), Is.Null);
        }

        [Test]
        public void DeleteGroup_When_User_Doesnot_Have_Permissions_For_Edit_Questionnaire_Then_DomainException_should_be_thrown()
        {
            // arrange
            Guid groupPublicKey = Guid.NewGuid();
            Questionnaire questionnaire = CreateQuestionnaireWithOneGroup(groupId: groupPublicKey, responsibleId: Guid.NewGuid());

            // act
            TestDelegate act = () => questionnaire.DeleteGroup(groupPublicKey, responsibleId: Guid.NewGuid());
            // assert
            var domainException = Assert.Throws<QuestionnaireException>(act);
            Assert.That(domainException.ErrorType, Is.EqualTo(DomainExceptionType.DoesNotHavePermissionsForEdit));
        }

        [Test]
        public void DeleteGroup_When_Questions_Of_Group__Is_not_involved_in_the_validations_and_conditions_of_other_questions_outside_the_group_Then_DomainException_should_NOT_be_thrown()
        {
            // arrange
            Guid groupId = Guid.NewGuid();
            Guid responsibleId = Guid.NewGuid();
            Questionnaire questionnaire = CreateQuestionnaireWithOneGroup(responsibleId: responsibleId,
                groupId: groupId);

            AddQuestion(questionnaire, Guid.NewGuid(), groupId, responsibleId, QuestionType.Text, "q1");

            // act
            TestDelegate act = () => questionnaire.DeleteGroup(groupId, responsibleId: responsibleId);

            // assert
            Assert.DoesNotThrow(act);
        }

        [Test]
        public void DeleteGroup_When_try_to_delete_last_section_with_cover_Then_DomainException_should_be_thrown()
        {
            // arrange
            Guid userSectionId = Guid.NewGuid();
            Guid responsibleId = Guid.NewGuid();
            var questionnaireDocument = CreateQuestionnaireDocument(children: new IComposite[]
                {
                    Create.Section(sectionId: Id.g1),   
                    Create.Section(sectionId: userSectionId)  
                });
            questionnaireDocument.CoverPageSectionId = Id.g1;

            var questionnaire = Create.Questionnaire(responsibleId, questionnaireDocument);

            // act
            TestDelegate act = () => questionnaire.DeleteGroup(userSectionId, responsibleId: responsibleId);

            // assert
            Assert.Catch<QuestionnaireException>(act);
        }

        [Test]
        public void DeleteGroup_When_try_to_delete_last_section_without_cover_Then_DomainException_should_be_thrown()
        {
            // arrange
            Guid userSectionId = Guid.NewGuid();
            Guid responsibleId = Guid.NewGuid();
            var questionnaireDocument = CreateQuestionnaireDocument(children: new IComposite[]
                {
                    Create.Section(sectionId: userSectionId)  
                });

            var questionnaire = Create.Questionnaire(responsibleId, questionnaireDocument);

            // act
            TestDelegate act = () => questionnaire.DeleteGroup(userSectionId, responsibleId: responsibleId);

            // assert
            Assert.Catch<QuestionnaireException>(act);
        }
    }
}