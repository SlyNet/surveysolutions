﻿using System;
using System.Collections.Generic;

namespace WB.Core.BoundedContexts.Designer.Views.Questionnaire.QuestionnaireList
{
    public class QuestionnaireListViewFolder : IQuestionnaireListItem
    {
        public QuestionnaireListViewFolder()
        {
            
        }

        public QuestionnaireListViewFolder(Guid publicId, string title)
        {
            PublicId = publicId;
            Title = title;
        }

        public virtual Guid PublicId { get; set; }

        public virtual string Title { get; set; } = string.Empty;

        public virtual Guid? Parent { get; set; }

        public virtual DateTime CreateDate { get; set; }

        public virtual string? CreatorName { get; set; }

        public virtual Guid CreatedBy { get; set; }

        public virtual int Depth { get; set; }

        public virtual string Path { get; set; } = String.Empty;

        //public virtual ICollection<QuestionnaireListViewItem> Questionnaires { get; set; } = null!;

        protected bool Equals(QuestionnaireListViewFolder other)
        {
            return PublicId.Equals(other.PublicId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((QuestionnaireListViewFolder) obj);
        }

        public override int GetHashCode()
        {
            return PublicId.GetHashCode();
        }
    }
}
