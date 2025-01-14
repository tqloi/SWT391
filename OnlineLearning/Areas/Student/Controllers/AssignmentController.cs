﻿using System.Linq;
using System.Security.Claims;
using Firebase.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Filter;
using OnlineLearning.Models;
using OnlineLearning.Models.ViewModel;
using OnlineLearning.Services;
using OnlineLearningApp.Respositories;

namespace OnlineLearning.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize]
    [Route("Student/[controller]/[action]")]
    public class AssignmentController : Controller
    {
        private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly DataContext _dataContext;
        private IConfiguration _configuration;
        private readonly FileService _fileService;

        public AssignmentController(DataContext dataContext, UserManager<AppUserModel> userManager, SignInManager<AppUserModel> signInManager, IWebHostEnvironment webHostEnvironment, IConfiguration configuration, FileService file)
        {
            _dataContext = dataContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _fileService = file;
        }
        public IActionResult Index()
        {
            return View();
        }
        
        [HttpGet]
        [ServiceFilter(typeof(AssignmentAccessFilter))]
        public async Task<IActionResult> SubmitAssignment(int id)
        {
            var assignment = await _dataContext.Assignment.FindAsync(id);
            var course = _dataContext.Courses.FirstOrDefault(c => c.CourseID == assignment.CourseID);
            ViewBag.Course = course;
            if (assignment == null)
            {
                return RedirectToAction("Index", "Home", new {Areas=""});
            }
            if(assignment.DueDate < DateTime.Now)
                {
                    TempData["warning"] = "Overdue";
                    return RedirectToAction("AssignmentList", "Participation", new { CourseID = course.CourseID });
                }
            var model = new SubmissionViewModel();
            model.AssignmentID = id;

            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> SubmitAssignment(SubmissionViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            var assignment = await _dataContext.Assignment.FirstOrDefaultAsync(a => a.AssignmentID == model.AssignmentID);
            var existedSubmition = await _dataContext.Submission.FirstOrDefaultAsync(p => p.AssignmentID == assignment.AssignmentID && p.StudentID == userId);
            var course = _dataContext.Courses.FirstOrDefault(c => c.CourseID == assignment.CourseID);
            ViewBag.Course = course;

            if (assignment == null) 
            { 
                return NotFound();
            }
            if (DateTime.Now > assignment.DueDate)
            {
                TempData["error"] = "Qua han nop bai";
                return RedirectToAction("AssignmentList", "Participation", new { Areas = "", CourseID = assignment.CourseID });
            }
            else
            {
                var submit = new SubmissionModel();
                if (model.SubmissionFile != null)
                {
                    string filename = model.SubmissionFile.FileName;       
                    try
                    {
                        string downloadUrl = await _fileService.UploadAssignment(model.SubmissionFile);             
                        submit.SubmissionLink = downloadUrl;
                        submit.FileName = filename;
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error uploading file: " + ex.Message);
                        TempData["error"] = "Edit failed due to file upload error!";
                        return View(model);
                    }               
                }
                if (model.SubmissionLink != null)
                {
                    submit.SubmissionLink = model.SubmissionLink;
                    submit.FileName = model.SubmissionLink;
                }

                submit.AssignmentID = model.AssignmentID;
                submit.StudentID = user.Id;
                submit.SubmissionDate = DateTime.Now;

                if (existedSubmition != null)
                {
                    _dataContext.Submission.Remove(existedSubmition);
                }

                TempData["success"] = "Submit successful!";
                _dataContext.Submission.Add(submit);
                await _dataContext.SaveChangesAsync();
            }        
            return RedirectToAction("AssignmentList", "Participation", new { Areas = "", CourseID = assignment.CourseID });
        }
    }
}
