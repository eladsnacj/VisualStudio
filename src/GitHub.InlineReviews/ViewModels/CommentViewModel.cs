﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Logging;
using GitHub.Models;
using GitHub.ViewModels;
using ReactiveUI;
using Serilog;

namespace GitHub.InlineReviews.ViewModels
{
    /// <summary>
    /// View model for an issue or pull request comment.
    /// </summary>
    public class CommentViewModel : ReactiveObject, ICommentViewModel
    {
        static readonly ILogger log = LogManager.ForContext<CommentViewModel>();
        string body;
        string errorMessage;
        bool isReadOnly;
        bool isSubmitting;
        CommentEditState state;
        DateTimeOffset updatedAt;
        string undoBody;
        bool canDelete;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommentViewModel"/> class.
        /// </summary>
        /// <param name="thread">The thread that the comment is a part of.</param>
        /// <param name="currentUser">The current user.</param>
        /// <param name="commentId">The GraphQL ID of the comment.</param>
        /// <param name="body">The comment body.</param>
        /// <param name="state">The comment edit state.</param>
        /// <param name="author">The author of the comment.</param>
        /// <param name="updatedAt">The modified date of the comment.</param>
        protected CommentViewModel(
            ICommentThreadViewModel thread,
            IActorViewModel currentUser,
            string commentId,
            string body,
            CommentEditState state,
            IActorViewModel author,
            DateTimeOffset updatedAt)
        {
            Guard.ArgumentNotNull(thread, nameof(thread));
            Guard.ArgumentNotNull(currentUser, nameof(currentUser));
            Guard.ArgumentNotNull(author, nameof(author));

            Thread = thread;
            CurrentUser = currentUser;
            Id = commentId;
            Body = body;
            EditState = state;
            Author = author;
            UpdatedAt = updatedAt;

            var canDelete = this.WhenAnyValue(
                x => x.EditState,
                x => x == CommentEditState.None && author.Login == currentUser.Login);

            canDelete.ToProperty(this, x => x.CanDelete);

            Delete = ReactiveCommand.CreateAsyncTask(canDelete, DoDelete);

            var canEdit = this.WhenAnyValue(
                x => x.EditState,
                x => x == CommentEditState.Placeholder || (x == CommentEditState.None && author.Login == currentUser.Login));

            BeginEdit = ReactiveCommand.Create(canEdit);
            BeginEdit.Subscribe(DoBeginEdit);
            AddErrorHandler(BeginEdit);

            CommitEdit = ReactiveCommand.CreateAsyncTask(
                Observable.CombineLatest(
                    this.WhenAnyValue(x => x.IsReadOnly),
                    this.WhenAnyValue(x => x.Body, x => !string.IsNullOrWhiteSpace(x)),
                    this.WhenAnyObservable(x => x.Thread.PostComment.CanExecuteObservable),
                    (readOnly, hasBody, canPost) => !readOnly && hasBody && canPost),
                DoCommitEdit);
            AddErrorHandler(CommitEdit);

            CancelEdit = ReactiveCommand.Create(CommitEdit.IsExecuting.Select(x => !x));
            CancelEdit.Subscribe(DoCancelEdit);
            AddErrorHandler(CancelEdit);

            OpenOnGitHub = ReactiveCommand.Create(this.WhenAnyValue(x => x.Id).Select(x => x != null));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommentViewModel"/> class.
        /// </summary>
        /// <param name="thread">The thread that the comment is a part of.</param>
        /// <param name="currentUser">The current user.</param>
        /// <param name="model">The comment model.</param>
        protected CommentViewModel(
            ICommentThreadViewModel thread,
            ActorModel currentUser,
            CommentModel model)
            : this(thread, new ActorViewModel(currentUser), model.Id, model.Body, CommentEditState.None, new ActorViewModel(model.Author), model.CreatedAt)
        {
        }

        protected void AddErrorHandler<T>(ReactiveCommand<T> command)
        {
            command.ThrownExceptions.Subscribe(x => ErrorMessage = x.Message);
        }

        async Task DoDelete(object unused)
        {
            try
            {
                ErrorMessage = null;
                IsSubmitting = true;

                await Thread.DeleteComment.ExecuteAsyncTask(Id);
            }
            catch (Exception e)
            {
                var message = e.Message;
                ErrorMessage = message;
                log.Error(e, "Error Deleting comment");
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        void DoBeginEdit(object unused)
        {
            if (state != CommentEditState.Editing)
            {
                undoBody = Body;
                EditState = CommentEditState.Editing;
            }
        }

        void DoCancelEdit(object unused)
        {
            if (EditState == CommentEditState.Editing)
            {
                EditState = string.IsNullOrWhiteSpace(undoBody) ? CommentEditState.Placeholder : CommentEditState.None;
                Body = undoBody;
                ErrorMessage = null;
                undoBody = null;
            }
        }

        async Task DoCommitEdit(object unused)
        {
            try
            {
                ErrorMessage = null;
                IsSubmitting = true;

                if (Id == null)
                {
                    await Thread.PostComment.ExecuteAsyncTask(Body);
                }
                else
                {
                    await Thread.EditComment.ExecuteAsyncTask(new Tuple<string, string>(Id, Body));
                }
            }
            catch (Exception e)
            {
                var message = e.Message;
                ErrorMessage = message;
                log.Error(e, "Error posting comment");
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        /// <inheritdoc/>
        public string Id { get; private set; }

        /// <inheritdoc/>
        public string Body
        {
            get { return body; }
            set { this.RaiseAndSetIfChanged(ref body, value); }
        }

        /// <inheritdoc/>
        public string ErrorMessage
        {
            get { return this.errorMessage; }
            private set { this.RaiseAndSetIfChanged(ref errorMessage, value); }
        }

        /// <inheritdoc/>
        public CommentEditState EditState
        {
            get { return state; }
            private set { this.RaiseAndSetIfChanged(ref state, value); }
        }

        /// <inheritdoc/>
        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set { this.RaiseAndSetIfChanged(ref isReadOnly, value); }
        }

        /// <inheritdoc/>
        public bool IsSubmitting
        {
            get { return isSubmitting; }
            protected set { this.RaiseAndSetIfChanged(ref isSubmitting, value); }
        }

        public bool CanDelete
        {
            get { return canDelete; }
            private set { this.RaiseAndSetIfChanged(ref canDelete, value); }
        }

        /// <inheritdoc/>
        public DateTimeOffset UpdatedAt
        {
            get { return updatedAt; }
            private set { this.RaiseAndSetIfChanged(ref updatedAt, value); }
        }

        /// <inheritdoc/>
        public IActorViewModel CurrentUser { get; }

        /// <inheritdoc/>
        public ICommentThreadViewModel Thread { get; }

        /// <inheritdoc/>
        public IActorViewModel Author { get; }

        /// <inheritdoc/>
        public ReactiveCommand<object> BeginEdit { get; }

        /// <inheritdoc/>
        public ReactiveCommand<object> CancelEdit { get; }

        /// <inheritdoc/>
        public ReactiveCommand<Unit> CommitEdit { get; }

        /// <inheritdoc/>
        public ReactiveCommand<object> OpenOnGitHub { get; }

        /// <inheritdoc/>
        public ReactiveCommand<Unit> Delete { get; }
    }
}
