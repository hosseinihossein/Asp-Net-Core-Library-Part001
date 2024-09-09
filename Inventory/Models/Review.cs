using Azure.Core.Pipeline;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Models;

public class Review_DbContext : DbContext
{
    public DbSet<Review_ReviewDbModel> Reviews { get; set; } = null!;
    public DbSet<Review_CommentDbModel> Comments { get; set; } = null!;
    public DbSet<Review_StarRatingDbModel> StarRatings { get; set; } = null!;

    public Review_DbContext(DbContextOptions<Review_DbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //***** One-to-Many, Review to Comments *****
        modelBuilder.Entity<Review_ReviewDbModel>()
        .HasMany<Review_CommentDbModel>(r => r.Comments)
        .WithOne(c => c.Review)
        .HasForeignKey(c => c.ReviewId)
        .IsRequired();

        //***** One-to-Many, Review to StarRating *****
        modelBuilder.Entity<Review_ReviewDbModel>()
        .HasMany<Review_StarRatingDbModel>(r => r.StarRatings)
        .WithOne(sr => sr.Review)
        .HasForeignKey(sr => sr.ReviewId)
        .IsRequired();

        //***** One-to-Many, Comment to ReplyComments *****
        modelBuilder.Entity<Review_CommentDbModel>()
        .HasMany<Review_CommentDbModel>(c => c.ReplyComments)
        .WithOne(c => c.ParentComment)
        .HasForeignKey(c => c.ParentCommentId)
        .IsRequired(false);
    }
}

public class Review_ReviewDbModel
{
    public int Id { get; set; }
    public string Guid { get; set; } = string.Empty;
    public List<string> LikedByClientsGuids { get; set; } = new();
    public List<string> ThumbsUpByClientsGuids { get; set; } = new();
    public List<string> ThumbsDownByClientsGuids { get; set; } = new();
    public List<Review_StarRatingDbModel> StarRatings { get; set; } = new();
    public List<Review_CommentDbModel> Comments { get; set; } = new();
}

public class Review_StarRatingDbModel
{
    public int Id { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string VoterGuid { get; set; } = string.Empty;
    public int Rate { get; set; } = 0;
    public int ReviewId { get; set; }
    public Review_ReviewDbModel Review { get; set; } = null!;
}

public class Review_CommentDbModel
{
    public int Id { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string WriterGuid { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public List<string> LikedByClientsGuids { get; set; } = new();
    public List<string> ThumbsUpByClientsGuids { get; set; } = new();
    public List<string> ThumbsDownByClientsGuids { get; set; } = new();
    public int ReviewId { get; set; }
    public Review_ReviewDbModel Review { get; set; } = null!;
    public List<Review_CommentDbModel> ReplyComments { get; set; } = new();
    public int? ParentCommentId { get; set; }
    public Review_CommentDbModel? ParentComment { get; set; }
}

public class Review_ReviewModel
{
    public string ReviewGuid { get; set; } = string.Empty;
    public List<string> LikeUsernames { get; set; } = new();
    public List<string> ThumbsUpUsernames { get; set; } = new();
    public List<string> ThumbsDownUsernames { get; set; } = new();
    public List<Review_StarRatingModel> StarRatings { get; set; } = new();
    public List<Review_CommentModel> CommentModels { get; set; } = new();
}

public class Review_StarRatingModel
{
    public string Guid { get; set; } = string.Empty;
    public string VoterUsername { get; set; } = string.Empty;
    public int Rate { get; set; } = 0;
}

public class Review_CommentModel
{
    public string CommentGuid { get; set; } = string.Empty;
    public string WriterUsername { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public List<string> LikeUsernames { get; set; } = new();
    public List<string> ThumbsUpUsernames { get; set; } = new();
    public List<string> ThumbsDownUsernames { get; set; } = new();
    public string? ParentCommentUsername { get; set; } = null;
}

public static class ReviewProcess
{
    public static async Task<Review_ReviewModel?> GetReviewModel(string reviewGuid, Review_DbContext reviewDb,
    UserManager<Identity_UserModel> userManager)
    {
        Review_ReviewDbModel? review = await reviewDb.Reviews
        .Include(r => r.StarRatings)
        .Include(r => r.Comments)
        .FirstOrDefaultAsync(r => r.Guid == reviewGuid);

        if (review is null)
        {
            return null;
        }

        Review_ReviewModel reviewModel = new()
        {
            //***** reviewModel.ReviewGuid *****
            ReviewGuid = review.Guid,
            //***** reviewModel.LikeUsernames *****
            LikeUsernames = await ReviewProcess.GetUsernamesList(review.LikedByClientsGuids, userManager),
            //***** reviewModel.ThumbsUpUsernames *****
            ThumbsUpUsernames = await ReviewProcess.GetUsernamesList(review.ThumbsUpByClientsGuids, userManager),
            //***** reviewModel.ThumbsDownUsernames *****
            ThumbsDownUsernames = await ReviewProcess.GetUsernamesList(review.ThumbsDownByClientsGuids, userManager)
        };

        //***** reviewModel.StarRatings *****
        List<Review_StarRatingModel> starRatingModelsList = new();
        foreach (Review_StarRatingDbModel starRating in review.StarRatings)
        {
            Identity_UserModel? voter = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == starRating.VoterGuid);
            if (voter is not null && voter.UserName is not null)
            {
                Review_StarRatingModel starRatingModel = new()
                {
                    Guid = starRating.Guid,
                    VoterUsername = voter.UserName,
                    Rate = starRating.Rate,
                };
                starRatingModelsList.Add(starRatingModel);
            }
        }
        reviewModel.StarRatings = starRatingModelsList;

        //***** reviewModel.CommentModels *****
        List<Review_CommentModel> commentModelsList = new();
        foreach (Review_CommentDbModel comment in review.Comments)
        {
            if (comment.ParentComment == null)
            {
                Identity_UserModel? writer =
                await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == comment.WriterGuid);
                if (writer is not null && writer.UserName is not null)
                {
                    Review_CommentModel commentModel = new()
                    {
                        CommentGuid = comment.Guid,
                        WriterUsername = writer.UserName,
                        CommentText = comment.CommentText,
                        Date = comment.Date,
                        LikeUsernames = await ReviewProcess.GetUsernamesList(comment.LikedByClientsGuids, userManager),
                        ThumbsUpUsernames = await ReviewProcess.GetUsernamesList(comment.ThumbsUpByClientsGuids, userManager),
                        ThumbsDownUsernames = await ReviewProcess.GetUsernamesList(comment.ThumbsDownByClientsGuids, userManager),
                        ParentCommentUsername = null
                    };
                    commentModelsList.Add(commentModel);
                    commentModelsList.AddRange(
                        await ReviewProcess.GetSubsequentReplyCommentModels(comment.Guid, writer.UserName, userManager, reviewDb));
                }
            }
        }
        reviewModel.CommentModels = commentModelsList;

        return reviewModel;
    }

    public static async Task<List<string>> GetUsernamesList(List<string> userGuidList,
    UserManager<Identity_UserModel> userManager)
    {
        List<string> usernamesList = new();
        foreach (string guid in userGuidList)
        {
            Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == guid);
            if (user is not null && user.UserName is not null)
            {
                usernamesList.Add(user.UserName);
            }
        }
        return usernamesList;
    }

    public static async Task<List<Review_CommentModel>> GetSubsequentReplyCommentModels(string commentGuid,
    string commentWriterUsername, UserManager<Identity_UserModel> userManager, Review_DbContext reviewDb)
    {
        List<Review_CommentModel> commentModelsList = new();
        Review_CommentDbModel? comment = await reviewDb.Comments.Include(c => c.ReplyComments)
        .FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is not null && comment.ReplyComments.Count > 0)
        {
            foreach (Review_CommentDbModel replyComment in comment.ReplyComments)
            {
                Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == replyComment.WriterGuid);
                if (user is not null && user.UserName is not null)
                {
                    Review_CommentModel commentModel = new()
                    {
                        CommentGuid = replyComment.Guid,
                        WriterUsername = user.UserName,
                        CommentText = replyComment.CommentText,
                        Date = replyComment.Date,
                        LikeUsernames = await GetUsernamesList(replyComment.LikedByClientsGuids, userManager),
                        ThumbsUpUsernames = await GetUsernamesList(replyComment.ThumbsUpByClientsGuids, userManager),
                        ThumbsDownUsernames = await GetUsernamesList(replyComment.ThumbsDownByClientsGuids, userManager),
                        ParentCommentUsername = commentWriterUsername
                    };
                    commentModelsList.Add(commentModel);
                    commentModelsList.AddRange(
                        await GetSubsequentReplyCommentModels(replyComment.Guid, user.UserName, userManager, reviewDb));
                }
            }
        }
        return commentModelsList;
    }

    public static async Task<List<Review_CommentDbModel>> GetSubsequentReplyComment_DbModels(string commentGuid,
    Review_DbContext reviewDb)
    {
        List<Review_CommentDbModel> commentDbModels = new();
        Review_CommentDbModel? comment = await reviewDb.Comments.Include(c => c.ReplyComments)
        .FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is not null && comment.ReplyComments.Count > 0)
        {
            foreach (Review_CommentDbModel replyComment in comment.ReplyComments)
            {
                commentDbModels.Add(comment);
                commentDbModels.AddRange(await GetSubsequentReplyComment_DbModels(comment.Guid, reviewDb));
            }
        }
        return commentDbModels;
    }

}

//*********************** backup ************************
public class Review_Review_BackupModel
{
    public string Guid { get; set; } = string.Empty;
    public List<string> LikedByClientsGuids { get; set; } = new();
    public List<string> ThumbsUpByClientsGuids { get; set; } = new();
    public List<string> ThumbsDownByClientsGuids { get; set; } = new();
}
public class Review_Comment_BackupModel
{
    public string Guid { get; set; } = string.Empty;
    public string WriterGuid { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public List<string> LikedByClientsGuids { get; set; } = new();
    public List<string> ThumbsUpByClientsGuids { get; set; } = new();
    public List<string> ThumbsDownByClientsGuids { get; set; } = new();
    public string? ParentCommentGuid { get; set; }
}
public class Review_StarRating_BackupModel
{
    public string Guid { get; set; } = string.Empty;
    public string VoterGuid { get; set; } = string.Empty;
    public int Rate { get; set; } = 0;
}

