using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Review.Models;

namespace Review.Controllers;
[AutoValidateAntiforgeryToken]
public class ReviewController : Controller
{
    readonly UserManager<Identity_UserModel> userManager;
    readonly IWebHostEnvironment env;
    readonly Review_DbContext reviewDb;
    readonly char ds = Path.DirectorySeparatorChar;
    public ReviewController(UserManager<Identity_UserModel> userManager, IWebHostEnvironment env,
    Review_DbContext reviewDbContext)
    {
        this.userManager = userManager;
        this.env = env;
        reviewDb = reviewDbContext;
    }

    public async Task<IActionResult> Index(string reviewGuid = "testReview")
    {
        Review_ReviewModel? reviewModel = await ReviewProcess.GetReviewModel(reviewGuid, reviewDb, userManager);

        if (reviewModel is null)
        {
            return NotFound();
        }

        return View(reviewModel);
    }

    //*********************** Review *************************
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitCommentForReview(string reviewGuid, string commentText)
    {
        if (!ModelState.IsValid)
        {
            Console.WriteLine("********************************");
            Console.WriteLine("Bad request");
            Console.WriteLine("********************************");
            return BadRequest(ModelState);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            //return NotFound();
            ModelState.AddModelError("User", $"Couldn't find any user named: {User.Identity!.Name!}");
            return BadRequest(ModelState);
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            //return NotFound();
            ModelState.AddModelError("Review", $"Couldn't find any review with guid: {reviewGuid}");
            return BadRequest(ModelState);
        }

        Review_CommentDbModel newComment = new()
        {
            Guid = Guid.NewGuid().ToString().Replace("-", ""),
            WriterGuid = user.UserGuid,
            CommentText = commentText,
            Date = DateTime.Now,
            Review = review
        };

        await reviewDb.Comments.AddAsync(newComment);
        await reviewDb.SaveChangesAsync();

        Console.WriteLine("********************************");
        Console.WriteLine("newComment.Guid: " + newComment.Guid);
        Console.WriteLine("********************************");

        return Content(newComment.Guid);
    }

    [Authorize]
    public async Task<IActionResult> LikeReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (!review.LikedByClientsGuids.Contains(user.UserGuid))
        {
            review.LikedByClientsGuids.Add(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnLikeReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (review.LikedByClientsGuids.Contains(user.UserGuid))
        {
            review.LikedByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> ThumbsUpReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (!review.ThumbsUpByClientsGuids.Contains(user.UserGuid))
        {
            review.ThumbsUpByClientsGuids.Add(user.UserGuid);
            review.ThumbsDownByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnThumbsUpReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (review.ThumbsUpByClientsGuids.Contains(user.UserGuid))
        {
            review.ThumbsUpByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> ThumbsDownReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (!review.ThumbsDownByClientsGuids.Contains(user.UserGuid))
        {
            review.ThumbsDownByClientsGuids.Add(user.UserGuid);
            review.ThumbsUpByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnThumbsDownReview(string reviewGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        if (review.ThumbsDownByClientsGuids.Contains(user.UserGuid))
        {
            review.ThumbsDownByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> StarRating(string reviewGuid, int rating)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_ReviewDbModel? review = await reviewDb.Reviews.Include(r => r.StarRatings).FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        Review_StarRatingDbModel? starRating = review.StarRatings.FirstOrDefault(sr => sr.VoterGuid == user.UserGuid);
        if (starRating is null)
        {
            starRating = new()
            {
                Guid = Guid.NewGuid().ToString().Replace("-", ""),
                VoterGuid = user.UserGuid,
                Rate = rating,
                Review = review
            };
            await reviewDb.StarRatings.AddAsync(starRating);
        }
        else
        {
            starRating.Rate = rating;
        }
        await reviewDb.SaveChangesAsync();

        return Ok();
    }

    //[Authorize]
    public async Task<IActionResult> GetLikesListForReview(string reviewGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(review.LikedByClientsGuids, userManager);
        return Json(usernamesList);
    }

    //[Authorize]
    public async Task<IActionResult> GetThumbsUpListForReview(string reviewGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(review.ThumbsUpByClientsGuids, userManager);
        return Json(usernamesList);
    }

    //[Authorize]
    public async Task<IActionResult> GetThumbsDownListForReview(string reviewGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(review.ThumbsDownByClientsGuids, userManager);
        return Json(usernamesList);
    }

    //[Authorize]
    public async Task<IActionResult> GetStarRatingListForReview(string reviewGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_ReviewDbModel? review = await reviewDb.Reviews.Include(r => r.StarRatings).FirstOrDefaultAsync(r => r.Guid == reviewGuid);
        if (review is null)
        {
            return NotFound();
        }

        var usernameRateList = review.StarRatings.Select(async sr =>
        new { username = (await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == sr.VoterGuid))?.UserName ?? "", rate = sr.Rate })
        .Select(t => t.Result).ToList();

        if (usernameRateList is null)
        {
            return BadRequest();
        }

        return Json(usernameRateList!);
    }

    //*********************** Comment *************************
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitCommentReply(string commentGuid, string replyText)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.Include(c => c.Review).FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        Review_CommentDbModel newComment = new()
        {
            Guid = Guid.NewGuid().ToString().Replace("-", ""),
            WriterGuid = user.UserGuid,
            CommentText = replyText,
            Date = DateTime.Now,
            Review = comment.Review,
            ParentComment = comment
        };

        await reviewDb.Comments.AddAsync(newComment);
        await reviewDb.SaveChangesAsync();

        return Content(newComment.Guid);
    }

    [Authorize]
    public async Task<IActionResult> LikeComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (!comment.LikedByClientsGuids.Contains(user.UserGuid))
        {
            comment.LikedByClientsGuids.Add(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnLikeComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (comment.LikedByClientsGuids.Contains(user.UserGuid))
        {
            comment.LikedByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> ThumbsUpComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(r => r.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (!comment.ThumbsUpByClientsGuids.Contains(user.UserGuid))
        {
            comment.ThumbsUpByClientsGuids.Add(user.UserGuid);
            comment.ThumbsDownByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnThumbsUpComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (comment.ThumbsUpByClientsGuids.Contains(user.UserGuid))
        {
            comment.ThumbsUpByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> ThumbsDownComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (!comment.ThumbsDownByClientsGuids.Contains(user.UserGuid))
        {
            comment.ThumbsDownByClientsGuids.Add(user.UserGuid);
            comment.ThumbsUpByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    [Authorize]
    public async Task<IActionResult> UnThumbsDownComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (comment.ThumbsDownByClientsGuids.Contains(user.UserGuid))
        {
            comment.ThumbsDownByClientsGuids.Remove(user.UserGuid);
            await reviewDb.SaveChangesAsync();
        }

        return Ok();
    }

    //[Authorize]
    public async Task<IActionResult> GetLikesListForComment(string commentGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(r => r.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(comment.LikedByClientsGuids, userManager);
        return Json(usernamesList);
    }

    //[Authorize]
    public async Task<IActionResult> GetThumbsUpListForComment(string commentGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(r => r.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(comment.ThumbsUpByClientsGuids, userManager);
        return Json(usernamesList);
    }

    //[Authorize]
    public async Task<IActionResult> GetThumbsDownListForComment(string commentGuid)
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }*/

        Review_CommentDbModel? comment = await reviewDb.Comments.FirstOrDefaultAsync(r => r.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        List<string> usernamesList = await ReviewProcess.GetUsernamesList(comment.ThumbsDownByClientsGuids, userManager);
        return Json(usernamesList);
    }

    [Authorize]
    public async Task<IActionResult> DeleteComment(string commentGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        Review_CommentDbModel? comment = await reviewDb.Comments
        .Include(c => c.ReplyComments)
        .Include(c => c.Review)
        .FirstOrDefaultAsync(r => r.Guid == commentGuid);
        if (comment is null)
        {
            return NotFound();
        }

        if (await userManager.IsInRoleAsync(user, "Review_Admins") || user.UserGuid == comment.WriterGuid)
        {
            reviewDb.Comments.Remove(comment);
            await reviewDb.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { reviewGuid = comment.Review.Guid });
        }

        return StatusCode(405);
    }

    /************************************ Backup ************************************/
    [Authorize(Roles = "Review_Admins")]
    public IActionResult Backup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
        string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
        if (System.IO.File.Exists(backupZipFilePath))
        {
            ViewBag.BackupDate = System.IO.File.GetCreationTime(backupZipFilePath);
        }

        ViewBag.ControllerName = "Review";
        return View();
    }

    [Authorize(Roles = "Review_Admins")]
    public async Task<IActionResult> RenewBackup()
    {
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
        if (!Directory.Exists(mainDirectoryPath))
        {
            Directory.CreateDirectory(mainDirectoryPath);
        }

        string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        if (Directory.Exists(dataDirectoryPath))
        {
            Directory.Delete(dataDirectoryPath, true);
        }
        Directory.CreateDirectory(dataDirectoryPath);

        string backupZipFilePath = $"{mainDirectoryPath}{ds}backup.zip";
        if (System.IO.File.Exists(backupZipFilePath))
        {
            System.IO.File.Delete(backupZipFilePath);
        }

        foreach (Review_ReviewDbModel reviewDbModel in
        await reviewDb.Reviews.Include(r => r.StarRatings).Include(r => r.Comments).ToListAsync())
        {
            Review_Review_BackupModel review_model = new()
            {
                Guid = reviewDbModel.Guid,
                LikedByClientsGuids = reviewDbModel.LikedByClientsGuids,
                ThumbsUpByClientsGuids = reviewDbModel.ThumbsUpByClientsGuids,
                ThumbsDownByClientsGuids = reviewDbModel.ThumbsDownByClientsGuids,
            };
            string review_json = JsonSerializer.Serialize(review_model);

            string reviewDirectoryPath = $"{dataDirectoryPath}{ds}{reviewDbModel.Guid}";
            Directory.CreateDirectory(reviewDirectoryPath);

            string review_jsonDataFilePath = $"{reviewDirectoryPath}{ds}reviewData.json";
            await System.IO.File.WriteAllTextAsync(review_jsonDataFilePath, review_json);

            foreach (Review_StarRatingDbModel starRatingDbModel in reviewDbModel.StarRatings)
            {
                Review_StarRating_BackupModel starRating_model = new()
                {
                    Guid = starRatingDbModel.Guid,
                    VoterGuid = starRatingDbModel.VoterGuid,
                    Rate = starRatingDbModel.Rate
                };
                string starRating_json = JsonSerializer.Serialize(starRating_model);

                string starRatingDirectoryPath = $"{reviewDirectoryPath}{ds}StarRatings{ds}{starRatingDbModel.Guid}";
                Directory.CreateDirectory(starRatingDirectoryPath);

                string starRating_jsonDataFilePath = $"{starRatingDirectoryPath}{ds}starRatingData.json";
                await System.IO.File.WriteAllTextAsync(starRating_jsonDataFilePath, starRating_json);
            }

            foreach (Review_CommentDbModel commentDbModel in reviewDbModel.Comments)
            {
                Review_Comment_BackupModel comment_model = new()
                {
                    Guid = commentDbModel.Guid,
                    WriterGuid = commentDbModel.WriterGuid,
                    CommentText = commentDbModel.CommentText,
                    Date = commentDbModel.Date,
                    LikedByClientsGuids = commentDbModel.LikedByClientsGuids,
                    ThumbsUpByClientsGuids = commentDbModel.ThumbsUpByClientsGuids,
                    ThumbsDownByClientsGuids = commentDbModel.ThumbsDownByClientsGuids,
                    ParentCommentGuid = commentDbModel.ParentComment?.Guid ?? null,
                };
                string comment_json = JsonSerializer.Serialize(comment_model);

                string commentDirectoryPath = $"{reviewDirectoryPath}{ds}Comments{ds}{commentDbModel.Guid}";
                Directory.CreateDirectory(commentDirectoryPath);

                string comment_jsonDataFilePath = $"{commentDirectoryPath}{ds}commentData.json";
                await System.IO.File.WriteAllTextAsync(comment_jsonDataFilePath, comment_json);
            }
        }

        ZipFile.CreateFromDirectory(dataDirectoryPath, backupZipFilePath);

        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = "Review_Admins")]
    public IActionResult DownloadBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
        if (Directory.Exists(backupDirectory))
        {
            string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                return PhysicalFile(backupZipFilePath, "Application/zip", "ReviewBackup.zip");
            }
        }
        object o = "backup file Not found!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize(Roles = "Review_Admins")]
    public IActionResult DeleteBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
        if (Directory.Exists(backupDirectory))
        {
            string backupDataDirectoryPath = $"{backupDirectory}{ds}Data";
            if (Directory.Exists(backupDataDirectoryPath))
            {
                Directory.Delete(backupDataDirectoryPath, true);
            }

            string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                System.IO.File.Delete(backupZipFilePath);
            }
        }

        return RedirectToAction(nameof(Backup));
    }

    [HttpPost]
    [Authorize(Roles = "Review_Admins")]
    public async Task<IActionResult> UploadBackup(IFormFile backupZipFile)
    {
        if (ModelState.IsValid)
        {
            string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
            if (!Directory.Exists(mainDirectoryPath))
            {
                Directory.CreateDirectory(mainDirectoryPath);
            }

            string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
            if (Directory.Exists(dataDirectoryPath))
            {
                Directory.Delete(dataDirectoryPath, true);
            }
            Directory.CreateDirectory(dataDirectoryPath);

            string backupZipFilePath = $"{mainDirectoryPath}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                System.IO.File.Delete(backupZipFilePath);
            }

            using (FileStream fs = System.IO.File.Create(backupZipFilePath))
            {
                await backupZipFile.CopyToAsync(fs);
            }

            ZipFile.ExtractToDirectory(backupZipFilePath, dataDirectoryPath);
        }
        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = "Review_Admins")]
    public async Task<IActionResult> RecoverLastBackup()
    {
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Review{ds}Backup";
        string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        DirectoryInfo dataDirectoryInfo = new DirectoryInfo(dataDirectoryPath);
        foreach (DirectoryInfo reviewDirInfo in dataDirectoryInfo.EnumerateDirectories())
        {
            string jsonDataFilePath = $"{reviewDirInfo.FullName}{ds}reviewData.json";
            string review_json = await System.IO.File.ReadAllTextAsync(jsonDataFilePath, Encoding.UTF8);
            Review_Review_BackupModel? review_model =
            JsonSerializer.Deserialize<Review_Review_BackupModel>(review_json);
            if (review_model is null)
            {
                continue;
            }
            Review_ReviewDbModel? reviewDbModel =
            await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == review_model.Guid);
            if (reviewDbModel == null)
            {
                reviewDbModel = new()
                {
                    Guid = review_model.Guid,
                    LikedByClientsGuids = review_model.LikedByClientsGuids,
                    ThumbsUpByClientsGuids = review_model.ThumbsUpByClientsGuids,
                    ThumbsDownByClientsGuids = review_model.ThumbsDownByClientsGuids,
                };
                await reviewDb.Reviews.AddAsync(reviewDbModel);
                await reviewDb.SaveChangesAsync();
            }
            string starRatingsDirectoryPath = $"{reviewDirInfo.FullName}{ds}StarRatings";
            if (Directory.Exists(starRatingsDirectoryPath))
            {
                DirectoryInfo starRatingsDirInfo = new DirectoryInfo(starRatingsDirectoryPath);
                foreach (DirectoryInfo starRatingDirInfo in starRatingsDirInfo.EnumerateDirectories())
                {
                    string starRating_jsonDataFilePath = $"{starRatingDirInfo.FullName}{ds}starRatingData.json";
                    string starRating_json = await System.IO.File.ReadAllTextAsync(starRating_jsonDataFilePath, Encoding.UTF8);
                    Review_StarRating_BackupModel? starRating_model =
                    JsonSerializer.Deserialize<Review_StarRating_BackupModel>(starRating_json);
                    if (starRating_model is null)
                    {
                        continue;
                    }
                    string starRatingGuid = starRating_model.Guid;
                    Review_StarRatingDbModel? starRatingDbModel =
                    await reviewDb.StarRatings.FirstOrDefaultAsync(sr => sr.Guid == starRatingGuid);
                    if (starRatingDbModel is null)
                    {
                        starRatingDbModel = new()
                        {
                            Guid = starRating_model.Guid,
                            VoterGuid = starRating_model.VoterGuid,
                            Rate = starRating_model.Rate,
                            Review = reviewDbModel
                        };
                        await reviewDb.StarRatings.AddAsync(starRatingDbModel);
                        await reviewDb.SaveChangesAsync();
                        //reviewDbModel.StarRatings.Add(starRatingDbModel);
                    }
                }
            }

            string commentsDirectoryPath = $"{reviewDirInfo.FullName}{ds}Comments";
            if (Directory.Exists(commentsDirectoryPath))
            {
                DirectoryInfo commentsDirInfo = new DirectoryInfo(commentsDirectoryPath);
                foreach (DirectoryInfo commentDirInfo in commentsDirInfo.EnumerateDirectories())
                {
                    string comment_jsonDataFilePath = $"{commentDirInfo.FullName}{ds}commentData.json";
                    string comment_json = await System.IO.File.ReadAllTextAsync(comment_jsonDataFilePath);
                    Review_Comment_BackupModel? comment_model =
                    JsonSerializer.Deserialize<Review_Comment_BackupModel>(comment_json);
                    if (comment_model is null)
                    {
                        continue;
                    }
                    string commentGuid = comment_model.Guid;
                    Review_CommentDbModel? commentDbModel = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == commentGuid);
                    if (commentDbModel is null)
                    {
                        commentDbModel = new()
                        {
                            Guid = comment_model.Guid,
                            WriterGuid = comment_model.WriterGuid,
                            CommentText = comment_model.CommentText,
                            Date = comment_model.Date,
                            LikedByClientsGuids = comment_model.LikedByClientsGuids,
                            ThumbsUpByClientsGuids = comment_model.ThumbsUpByClientsGuids,
                            ThumbsDownByClientsGuids = comment_model.ThumbsDownByClientsGuids,
                            Review = reviewDbModel
                        };
                        await reviewDb.Comments.AddAsync(commentDbModel);
                        await reviewDb.SaveChangesAsync();
                        //reviewDbModel.Comments.Add(commentDbModel);
                    }
                }
            }
            //}
            //reviewDbModels.Add(reviewDbModel);
        }
        //await reviewDb.Reviews.AddRangeAsync(reviewDbModels);
        //await reviewDb.SaveChangesAsync();

        //string mainDirectoryPath = $"{env.ContentRootPath}{ds}Backups{ds}Reviews";
        //string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        //DirectoryInfo dataDirectoryInfo = new DirectoryInfo(dataDirectoryPath);
        foreach (DirectoryInfo reviewDirInfo in dataDirectoryInfo.EnumerateDirectories())
        {
            string commentsDirectoryPath = $"{reviewDirInfo.FullName}{ds}Comments";
            if (!Directory.Exists(commentsDirectoryPath))
            {
                continue;
            }
            DirectoryInfo commentsDirInfo = new DirectoryInfo(commentsDirectoryPath);
            foreach (DirectoryInfo commentDirInfo in commentsDirInfo.EnumerateDirectories())
            {
                string comment_jsonDataFilePath = $"{commentDirInfo.FullName}{ds}commentData.json";
                string comment_json = await System.IO.File.ReadAllTextAsync(comment_jsonDataFilePath);
                Review_Comment_BackupModel? comment_model =
                JsonSerializer.Deserialize<Review_Comment_BackupModel>(comment_json);
                if (comment_model is null)
                {
                    continue;
                }
                if (comment_model.ParentCommentGuid == null)
                {
                    continue;
                }
                Review_CommentDbModel? commentDbModel = await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == comment_model.Guid);
                if (commentDbModel is not null)
                {
                    Review_CommentDbModel? parentCommentDbModel =
                    await reviewDb.Comments.FirstOrDefaultAsync(c => c.Guid == comment_model.ParentCommentGuid);

                    if (parentCommentDbModel is not null)
                    {
                        //parentCommentDbModel.ReplyComments.Add(commentDbModel);
                        commentDbModel.ParentComment = parentCommentDbModel;
                    }
                }
            }
        }
        await reviewDb.SaveChangesAsync();

        ViewBag.ResultState = "success";
        object o = $"Recovery for Review has done successfully.";
        return View("Result", o);
    }

}