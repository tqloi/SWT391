﻿using Firebase.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Controllers;
using OnlineLearning.Models;
using OnlineLearning.Models.ViewModel;
using OnlineLearning.Services;
using OnlineLearningApp.Respositories;
using System.Security.Claims;

namespace OnlineLearning.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    [Route("Instructor/[controller]/[action]")]

    [Authorize(Roles = "Instructor")]
    public class CourseController : Controller
    {
        private readonly DataContext datacontext;
        private UserManager<AppUserModel> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly FileService _fileService;

        public CourseController(DataContext context, UserManager<AppUserModel> userManager, IWebHostEnvironment webHostEnvironment, FileService fileService)
        {
            datacontext = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _fileService = fileService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CourseViewModel model)
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            var existingCourse = await datacontext.Courses.FirstOrDefaultAsync(c => c.CourseCode == model.CourseCode);

            if (existingCourse != null)
            {
                TempData["warning"] = "Course code already exists!";
                return RedirectToAction("MyCourse", "Course", new { area = "Instructor" });
            }

            var course = new CourseModel
            {
                Title = model.Title,
                Description = model.Description,
                CourseCode = model.CourseCode,
                CategoryID = model.CategoryId,
                Level = model.Level,
                EndDate = model.EndDate,
                Price = model.Price,
                CreateDate = DateTime.Now,
                LastUpdate = DateTime.Now,
                NumberOfStudents = 0,
                NumberOfRate = 0,
                InstructorID = userId,
                Status = false
            };
 
            if (model.CoverImage != null)
            {
                try
                {
                    string downloadUrl = await _fileService.UploadImage(model.CoverImage);
                    course.CoverImagePath = downloadUrl;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error uploading file: " + ex.Message);
                    return RedirectToAction("MyCourse", "Course", new { area = "Instructor" });
                }
            }
            else { course.CoverImagePath = "/Images/faq_graphic.jpg"; }

            datacontext.Courses.Add(course);
            await datacontext.SaveChangesAsync();

            int newCourseId = course.CourseID;

            if (model.CourseMaterials != null && model.CourseMaterials.Count > 0)
            {
                foreach (var file in model.CourseMaterials)
                {
                    var material = new CourseMaterialModel();
                    string fileName = file.FileName;
                    try
                    {
                        string downloadUrl = await _fileService.UploadCourseDocument(file);
                        material.MaterialsLink = downloadUrl;
                        material.CourseID = newCourseId;
                        material.FIleName = fileName;
                        material.fileExtension = Path.GetExtension(fileName);

                        datacontext.CourseMaterials.Add(material);
                        await datacontext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error uploading file: " + ex.Message);
                        TempData["error"] = "Edit failed due to file upload error!";
                        return RedirectToAction("MyCourse", "Course", new { area = "Instructor" });
                    }
                }
            }
            var notify = new NotificationModel
            {
                UserId = user.Id,
                Description = $"{user.FirstName} {user.LastName} has just created a course named: {course.Title}",
                CreatedAt = DateTime.Now
            };
            datacontext.Notification.Add(notify);
            await datacontext.SaveChangesAsync();
            TempData["success"] = "Course created successfully!";
            //return RedirectToAction("Index", "Instructor", new { area = "Instructor" });
            return RedirectToAction("Dashboard", "Instructor", new { area = "Instructor", CourseID = newCourseId });
        }

        [HttpPost]
        public async Task<IActionResult> Update(CourseViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var course = await datacontext.Courses.FindAsync(model.CourseID);

            var existingCourse = await datacontext.Courses
                      .FirstOrDefaultAsync(c => c.CourseCode == model.CourseCode && c.CourseID != model.CourseID);

            if (existingCourse != null)
            {
                TempData["warning"] = "Course code already exists!";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            if (course == null)
            {
                return NotFound();
            }
            course.Title = model.Title;
            course.Description = model.Description;
            course.CourseCode = model.CourseCode;
            course.Price = model.Price;

            if (model.CategoryID != 0)
            {
                course.CategoryID = model.CategoryID;
            }
            if (model.Level != "none")
            {
                course.Level = model.Level;
            }

            course.EndDate = new DateTime(2030, 1, 1);
            course.LastUpdate = DateTime.Now;

            if (model.CoverImage != null)
            {
                try
                {
                    //await _fileService.DeleteFile(course.CoverImagePath);
                    string downloadUrl = await _fileService.UploadImage(model.CoverImage);
                    course.CoverImagePath = downloadUrl;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error uploading file: " + ex.Message);
                    TempData["error"] = "Edit failed due to file upload error!";
                    return Redirect(Request.Headers["Referer"].ToString());
                }
            }

            await datacontext.SaveChangesAsync();

            TempData["success"] = "Update created successfully!";
            //return RedirectToAction("Index", "Instructor", new { area = "Instructor" });
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int CourseId)
        {
            var course = await datacontext.Courses.FindAsync(CourseId);
            if (course == null)
            {
                TempData["error"] = "Course not found!";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            var deleteAllowedDate = course.LastUpdate.AddDays(30);
            if (deleteAllowedDate > DateTime.Now)
            {
                var daysRemaining = (deleteAllowedDate - DateTime.Now).Days;
                TempData["warning"] = $"Can be deleted after {daysRemaining} days.";
                return Redirect(Request.Headers["Referer"].ToString());
            }
            datacontext.Courses.Remove(course);
            await datacontext.SaveChangesAsync();

            TempData["success"] = "Course deleted successfully!";
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        public async Task<IActionResult> SetSate(int CourseId)
        {
            var course = await datacontext.Courses.FindAsync(CourseId);
            if (course == null)
            {
                TempData["error"] = "Course not found!";
                return Redirect(Request.Headers["Referer"].ToString());
            }
            if (course.Status == false && course.IsBaned == true) 
            {
                TempData["warning"] = "Cannot enable because the course violates the terms!";
                return Redirect(Request.Headers["Referer"].ToString());
            }

            if (course.Status == false)
            {
                var lectures = await datacontext.Lecture.Where(l => l.CourseID == CourseId).ToArrayAsync();
                var tests = await datacontext.Test.Where(t => t.CourseID == CourseId).ToArrayAsync();
                var assignments = await datacontext.Assignment.Where(a => a.CourseID == CourseId).ToArrayAsync();
                var courseMaterials = await datacontext.CourseMaterials.Where(a => a.CourseID == CourseId).ToArrayAsync();
                if (!lectures.Any() || !tests.Any() || !assignments.Any() || !courseMaterials.Any()) 
                {
                    TempData["warning"] = "Please add more course content";
                    return Redirect(Request.Headers["Referer"].ToString());
                }
            }

            course.Status = !course.Status;
            course.LastUpdate = DateTime.Now;
            await datacontext.SaveChangesAsync();

            TempData["success"] = course.Status ? "Course enabled successfully!" : "Course disable successfully!";
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpGet]
        public async Task<IActionResult> MyCourse(int? category = null, string level = null, int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var coursesQuery = datacontext.Courses
                    .Where(course => course.InstructorID == userId)
                    .Include(course => course.Instructor)
                    .ThenInclude(instructor => instructor.AppUser)
                    .OrderByDescending(sc => sc.CourseID)
                    .AsQueryable();

            // Lọc theo category nếu có
            if (category.HasValue)
            {
                coursesQuery = coursesQuery.Where(course => course.CategoryID == category.Value);
            }

            // Lọc theo level nếu có
            if (!string.IsNullOrEmpty(level))
            {
                coursesQuery = coursesQuery.Where(course => course.Level == level);
            }

            var totalCourses = await coursesQuery.CountAsync();
            var pageSize = 5;
            var courses = await coursesQuery
                .OrderByDescending(course => course.Rating)
                .ThenByDescending(course => course.NumberOfRate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new ListViewModel
            {
                Courses = courses,
                TotalPage = (int)Math.Ceiling(totalCourses / (double)pageSize),
                CurrentPage = page,
                Category = category,
                Level = level
            };

            return View(model);
        }
    }
}
