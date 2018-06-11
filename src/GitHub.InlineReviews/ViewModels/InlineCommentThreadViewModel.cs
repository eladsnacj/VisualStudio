﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Models;
using GitHub.Services;
using ReactiveUI;

namespace GitHub.InlineReviews.ViewModels
{
    /// <summary>
    /// A thread of inline comments (aka Pull Request Review Comments).
    /// </summary>
    public class InlineCommentThreadViewModel : CommentThreadViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InlineCommentThreadViewModel"/> class.
        /// </summary>
        /// <param name="session">The current PR review session.</param>
        /// <param name="comments">The comments to display in this inline review.</param>
        public InlineCommentThreadViewModel(
            IPullRequestSession session,
            IEnumerable<InlineCommentModel> comments)
            : base(session.User)
        {
            Guard.ArgumentNotNull(session, nameof(session));

            Session = session;

            PostComment = ReactiveCommand.CreateAsyncTask(
                Observable.Return(true),
                DoPostComment);

            EditComment = ReactiveCommand.CreateAsyncTask(
                Observable.Return(true),
                DoEditComment);

            DeleteComment = ReactiveCommand.CreateAsyncTask(
                Observable.Return(true),
                DoDeleteComment);

            foreach (var comment in comments)
            {
                Comments.Add(new PullRequestReviewCommentViewModel(session, this, CurrentUser, comment.Comment));
            }

            Comments.Add(PullRequestReviewCommentViewModel.CreatePlaceholder(session, this, CurrentUser));
        }

        /// <summary>
        /// Gets the current pull request review session.
        /// </summary>
        public IPullRequestSession Session { get; }

        /// <inheritdoc/>
        public override Uri GetCommentUrl(int id)
        {
            return new Uri(string.Format(
                CultureInfo.InvariantCulture,
                "{0}/pull/{1}#discussion_r{2}",
                Session.LocalRepository.CloneUrl.ToRepositoryUrl(Session.RepositoryOwner),
                Session.PullRequest.Number,
                id));
        }

        async Task DoPostComment(object parameter)
        {
            Guard.ArgumentNotNull(parameter, nameof(parameter));

            var body = (string)parameter;
            var replyId = Comments[0].Id;
            await Session.PostReviewComment(body, replyId);
        }

        async Task DoEditComment(object parameter)
        {
            Guard.ArgumentNotNull(parameter, nameof(parameter));

            var item = (Tuple<string, string>)parameter;
            await Session.EditComment(item.Item1, item.Item2);
        }

        async Task DoDeleteComment(object parameter)
        {
            Guard.ArgumentNotNull(parameter, nameof(parameter));

            var number = (int)parameter;
            await Session.DeleteComment(number);
        }
    }
}
