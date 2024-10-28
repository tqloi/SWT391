﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnlineLearning.Controllers;
using OnlineLearning.Models;
using OnlineLearningApp.Respositories;
using System.Diagnostics;

namespace OnlineLearning.Areas.Instructor.Controllers
{
    [Authorize]
    [Area("Instructor")]
    [Route("Instructor/[controller]/[action]")]
    public class TestController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DataContext datacontext;
        private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public TestController(ILogger<HomeController> logger, DataContext context, SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager, IWebHostEnvironment webHostEnvironment)
        {
            datacontext = context;
            _logger = logger;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }
        //currently not in use yet
        public IActionResult Index(int CourseID)
        {
            ViewBag.CourseID = CourseID;
            var Course = datacontext.Courses.Find(CourseID);
            //absolutely useless foking line but i add it just for fun
            ViewBag.Course = Course;
            return View();
        }
        [HttpGet]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CreateTest(int CourseID)
        {
            ViewBag.CourseID = CourseID;
            var course = await datacontext.Courses.FindAsync(CourseID);

            if (course == null)
            {
                return NotFound();
            }

            ViewBag.Course = course;
            return View();
        }

        [HttpPost]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CreateTest(TestModel model)
        {
            var Course = datacontext.Courses.Find(model.CourseID);
            ViewBag.Course = Course;

            if (Course == null)
            {
                return NotFound();
            }
            //fucku viewbag shit
            if (model.Course == null)
            {
                model.Course = Course;
            }
            // if (ModelState.IsValid) invalid as always
            try
            {
                //handle null from view
                if (model.PassingScore == null)
                {
                    model.PassingScore = 5.0;
                }
                if (model.AlowRedo == null)
                {
                    model.AlowRedo = "Yes";
                }
                if (model.NumberOfMaxAttempt == null)
                {
                    model.NumberOfMaxAttempt = 1;
                }

                Debug.WriteLine("ID retrieved valid");
                var newTest = new TestModel
                {
                    AlowRedo = model.AlowRedo,
                    NumberOfMaxAttempt = model.NumberOfMaxAttempt,
                    Title = model.Title,
                    Course = model.Course,
                    Description = model.Description,
                    StartTime = model.StartTime,
                    EndTime = model.EndTime,
                    Status = model.Status,
                    CourseID = model.CourseID,
                    NumberOfQuestion = 0,
                    PassingScore = model.PassingScore
                };
                // Add the test to the context and save changes
                datacontext.Test.Add(newTest);
                await datacontext.SaveChangesAsync();
                Debug.WriteLine("Test saved to database");
                ViewBag.CourseID = Course.CourseID;
                TempData["success"] = "Test created successfully!";
                //keep it alive for 2 request 
                TempData.Keep();
                return RedirectToAction("CreateQuestionRedirector", "Question", new { TestID = newTest.TestID });
            }

            catch (Exception)
            {
                TempData["error"] = "Test creation failed!";
                TempData.Keep();
                return RedirectToAction("CreateTestRedirector", new { CourseID = Course.CourseID });
            }
        }
        [HttpGet]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public IActionResult EditTest(int TestID)
        {
            var Test = datacontext.Test.FirstOrDefault(t => t.TestID == TestID);
            var Course = datacontext.Courses.FirstOrDefault(c => c.CourseID == Test.CourseID);

            ViewBag.CourseID = Test.CourseID;
            ViewBag.Course = Course;
            if (Test == null)
            {
                return NotFound();
            }
            return View("EditTest", Test);
        }

        [HttpPost]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public IActionResult EditTest(TestModel model)
        {
            if (model == null)
            {
                return NotFound();
            }
            var submissions = datacontext.Score
                .Where(s => s.TestID == model.TestID)
                .ToList();
            foreach (var sub in submissions)
            {
                if (sub.NumberOfAttempt > model.NumberOfMaxAttempt)
                {
                    TempData["error"] = $"Cannot change Number Of Attempt Lower since " +
                        $"there's a student with {sub.NumberOfAttempt} submission";
                    TempData.Keep();
                    return RedirectToAction("TestList", "Participation", new { CourseID = model.CourseID });
                }
            }

            ViewBag.CourseID = model.CourseID;
            ViewBag.Course = model.Course;
            datacontext.Update(model);
            datacontext.SaveChanges();
            TempData["success"] = "Edit Successfully";
            TempData.Keep();
            return RedirectToAction("TestList", "Participation", new { CourseID = model.CourseID });
        }
        [HttpPost]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DeleteTest(int TestID)
        {
            var test = await datacontext.Test.FirstOrDefaultAsync(t => t.TestID == TestID);

            if (test == null)
            {
                return NotFound();
            }

            var questions = datacontext.Question
                .Where(q => q.TestID == TestID)
                .ToList();

            if (questions.Any())
            {
                var scores = datacontext.Score
                    .Where(s => s.TestID == TestID)
                    .ToList();
                if (scores.Any())
                {
                    //currently not allow delete test if there's a submission from a student
                    TempData["error"] = "Test already been done by students";
                    TempData.Keep();
                    return RedirectToAction("TestList", "Participation", new { CourseID = test.CourseID });
                }
                //delete image first
                foreach (var question in questions)
                {
                    if (!string.IsNullOrEmpty(question.ImagePath))
                    {
                        string ImageFullPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "QuestionImages", question.ImagePath);
                        if (System.IO.File.Exists(ImageFullPath))
                        {
                            System.IO.File.Delete(ImageFullPath);
                        }
                    }
                }
                // Remove associated questions first
                datacontext.Question.RemoveRange(questions);
            }

            // Delete the test after removing associated questions
            datacontext.Test.Remove(test);
            datacontext.SaveChanges();

            // Redirect to the test list or any other appropriate action
            TempData["success"] = "Test deletion successfully";
            TempData.Keep();
            return RedirectToAction("TestList", "Participation", new { CourseID = test.CourseID });
        }

        [HttpPost]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public IActionResult ClearAllQuestions(int TestID)
        {
            var test = datacontext.Test.FirstOrDefault(t => t.TestID == TestID);
            var questions = datacontext.Question
                .Where(q => q.TestID == TestID)
                .ToList();
            if (test == null)
            {
                return NotFound();
            }
            foreach (var question in questions)
            {
                if (!question.ImagePath.IsNullOrEmpty())
                {
                    string ImageFullPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "QuestionImages", question.ImagePath);
                    if (System.IO.File.Exists(ImageFullPath))
                    {
                        System.IO.File.Delete(ImageFullPath);
                    }
                }
            }
            datacontext.Question.RemoveRange(questions);
            datacontext.SaveChanges();
            TempData["success"] = "Clear all questions successfully";
            TempData.Keep();
            var Course = datacontext.Courses.FirstOrDefault(c => c.CourseID == test.CourseID);
           // ViewBag.CourseID = test.CourseID;
           ViewBag.Course = Course;    
            return RedirectToAction("TestList", "Participation", new { CourseID = test.CourseID });
        }
        [HttpPost]
        [Area("Instructor")]
        [Authorize(Roles = "Instructor")]
        public IActionResult ClearAllSubmission(int TestID)
        {
            var test = datacontext.Test.FirstOrDefault(t => t.TestID == TestID);
            if (test == null)
            {
                return NotFound();
            }
            var submissions = datacontext.Score
                .Where(s => s.TestID == TestID)
                .ToList();
            datacontext.Score.RemoveRange(submissions);
            datacontext.SaveChanges();
            TempData["success"] = "Clear all submission successfully";
            TempData.Keep();
            var Course = datacontext.Courses.FirstOrDefault(c => c.CourseID == test.CourseID);
            // ViewBag.CourseID = test.CourseID;
            ViewBag.Course = Course;    
            return RedirectToAction("TestList", "Participation", new { CourseID = test.CourseID });
        }
    }
}