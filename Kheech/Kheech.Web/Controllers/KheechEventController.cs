﻿using Kheech.Web.Models;
using Kheech.Web.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Net;
using Microsoft.Ajax.Utilities;
using System.Threading.Tasks;
using Humanizer;
using Kheech.Web.Clients;
using System.Configuration;

namespace Kheech.Web.Controllers
{
    [RoutePrefix("Kheech")]
    [Authorize]
    public class KheechEventController : Controller
    {
        protected readonly ApplicationDbContext _context;

        public KheechEventController()
        {
            _context = new ApplicationDbContext();
        }

        // GET: KheechEvent
        [Route("", Name = "HomePage")]
        public async Task<ActionResult> Index()
        {
            var currentUserId = User.Identity.GetUserId();
            var currentUserName = User.Identity.GetUserName();

            var pendingFriends = await _context.InviteFriends.Include(f => f.ApplicationUser)
                                    .Include(f => f.FriendshipStatus)
                                    .Where(f => f.Email == currentUserName)
                                    .ToListAsync();

            if (pendingFriends.Count != 0)
            {
                ViewBag.Notification = pendingFriends.Count;
            }

            var currentDay = DateTime.UtcNow.Day;
            var kheechIndexViewModel = new KheechIndexViewModel();

            kheechIndexViewModel.TodayKheechEvents = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                   .Include(k => k.Location)
                                                   .Include(k => k.Group)
                                                   .Include(k => k.KheechUsers)
                                                   .Where(m => (m.ApplicationUserId == currentUserId) && (m.EndDate.Day == currentDay))
                                                   .Distinct().Take(5).ToListAsync();

            kheechIndexViewModel.ActiveKheechEvents = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                   .Include(k => k.Location)
                                                   .Include(k => k.Group)
                                                   .Include(k => k.KheechUsers)
                                                   .Where(m => (m.ApplicationUserId == currentUserId) &&
                                                               (m.EndDate > DateTime.UtcNow) &&
                                                               (m.EndDate.Day != currentDay))
                                                   .OrderByDescending(m => m.EndDate)
                                                   .Distinct().Take(5).ToListAsync();

            if ((kheechIndexViewModel.ActiveKheechEvents.Count() >= 0) && (kheechIndexViewModel.ActiveKheechEvents.Count() < 5))
            {
                var kheechUsers = await _context.KheechUsers.Include(k => k.KheechEvent.Location)
                                                      .Include(k => k.ApplicationUser)
                                                      .Where(k => (k.ApplicationUserId == currentUserId) && (k.KheechEvent.EndDate > DateTime.UtcNow))
                                                      .Distinct().Take(5).ToListAsync();
                foreach (var kuser in kheechUsers)
                {
                    if (kuser.KheechEvent.EndDate.Day == currentDay)
                    {
                        kheechIndexViewModel.TodayKheechEvents.Add(kuser.KheechEvent);
                        kheechIndexViewModel.TodayKheechEvents.DistinctBy(k => k.Id);
                    }
                    else
                    {
                        kheechIndexViewModel.ActiveKheechEvents.Add(kuser.KheechEvent);
                        kheechIndexViewModel.ActiveKheechEvents.DistinctBy(k => k.Id);
                    }
                }
            }

            if ((kheechIndexViewModel.ActiveKheechEvents.Count() == 0) && (kheechIndexViewModel.TodayKheechEvents.Count() == 0))
            {
                ViewBag.Message = "Hey, it looks like you have nothing on, for today. Would you like to schedule?";
            }

            kheechIndexViewModel.RecentSchedules = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                    .Include(k => k.Location)
                                                    .Include(k => k.Group)
                                                    .Where(k => k.ApplicationUserId == currentUserId && k.EndDate <= DateTime.UtcNow)
                                                    .OrderByDescending(k => k.EndDate)
                                                    .Take(3)
                                                    .ToListAsync();

            if (kheechIndexViewModel.RecentSchedules.Count < 3)
            {
                var recentKheechUsers = await _context.KheechUsers.Include(k => k.KheechEvent.Location)
                                      .Include(k => k.ApplicationUser)
                                      .Where(k => (k.ApplicationUserId == currentUserId) && (k.KheechEvent.EndDate < DateTime.UtcNow))
                                      .Distinct().Take(3).ToListAsync();

                foreach (var kuser in recentKheechUsers)
                {
                    kheechIndexViewModel.RecentSchedules.Add(kuser.KheechEvent);
                    kheechIndexViewModel.RecentSchedules.DistinctBy(k => k.Id);
                }
            }

            kheechIndexViewModel.RecentMoments = await _context.Moments.Include(m => m.KheechEvent.Location)
                                                                 .Where(m => m.ApplicationUserId == currentUserId)
                                                                 .OrderByDescending(m => m.InsertDate)
                                                                 .Take(3)
                                                                 .ToListAsync();

            kheechIndexViewModel.RecentFriends = _context.KheechUsers.Include(k => k.ApplicationUser)
                                                   .Include(k => k.KheechEvent.Location)
                                                   .Where(m => m.KheechEvent.ApplicationUserId == currentUserId)
                                                   .DistinctBy(k => k.ApplicationUserId).ToList();

            return View(kheechIndexViewModel);

        }

        // GET: KheechEvents/Details/5
        [Route("details/{id}", Name = "KheechDetails")]
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            KheechEvent kheechEvent = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                           .Include(k => k.KheechComments)
                                                           .Include(k => k.KheechUsers)
                                                           .Include(k => k.KheechMoments)
                                                           .Include(k => k.Location)
                                                           .Include(k => k.Group)
                                                           .FirstOrDefaultAsync(k => k.Id == id);

            if (kheechEvent == null)
            {
                return HttpNotFound();
            }

            var currentUserId = User.Identity.GetUserId();
            bool isRelatedKheech = false;

            if (kheechEvent.ApplicationUserId == currentUserId)
            {
                isRelatedKheech = true;
            }

            foreach (var user in kheechEvent.KheechUsers)
            {
                if (user.ApplicationUserId == currentUserId)
                {
                    isRelatedKheech = true;
                }
                var kheechUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.ApplicationUserId);
                if (kheechUser == null)
                {
                    return HttpNotFound();
                }

                user.ApplicationUser = kheechUser;
            }

            if (!isRelatedKheech)
            {
                TempData["NotRelated"] = "This kheech does not belongs to you. Hence, you cannot see the details";
                return RedirectToRoute("HomePage");
            }

            foreach (var comment in kheechEvent.KheechComments)
            {
                var commentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == comment.CreatorId);
                if (commentUser == null)
                {
                    return HttpNotFound();
                }

                comment.ApplicationUser = commentUser;
            }
            return View(kheechEvent);
        }

        [Route("schedule", Name = "ScheduleMeeting")]
        public async Task<ActionResult> Schedule()
        {
            var currentUserId = User.Identity.GetUserId();

            var scheduleViewModel = new ScheduleViewModel();

            //scheduleViewModel.Friends = Enumerable.Empty<Friendship>();

            var friends = await _context.Friendships
                        .Include(f => f.Initiator)
                        .Include(f => f.Recipient)
                        .Include(f => f.FriendshipStatus)
                        .Where(f => f.InitiatorId == currentUserId || f.RecipientId == currentUserId).Distinct().ToListAsync();

            foreach (var item in friends)
            {
                if (item.RecipientId == currentUserId)
                {
                    var friendViewModel1 = new FriendViewModel
                    {
                        Id = item.InitiatorId,
                        Name = item.Initiator.FirstName
                    };
                    scheduleViewModel.Friends.Add(friendViewModel1);
                }
                else
                {
                    var friendViewModel = new FriendViewModel
                    {
                        Id = item.RecipientId,
                        Name = item.Recipient.FirstName
                    };
                    scheduleViewModel.Friends.Add(friendViewModel);
                }
            }

            return View(scheduleViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("schedule/{id}", Name = "ScheduleMeetingPost")]
        public async Task<ActionResult> Schedule(int id, ScheduleViewModel model)
        {
            var currentUserId = User.Identity.GetUserId();
            var kheechEvent = new KheechEvent();

            kheechEvent.EventName = model.EventName;
            kheechEvent.ApplicationUserId = currentUserId;
            kheechEvent.StartDate = model.WhenToMeet;
            kheechEvent.EndDate = kheechEvent.StartDate.AddHours(2);
            kheechEvent.IsGroupEvent = false;
            kheechEvent.InsertDate = DateTime.UtcNow;

            var googleLocation = model.WhereToMeet.Split(',').ToList();
            var locationToMeet = googleLocation[0];
            var Counter = googleLocation.Count;

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Name == locationToMeet);
            if (location == null)
            {
                location = new Location
                {
                    Name = locationToMeet,
                    address1 = googleLocation[Counter - 4],
                    Country = googleLocation[Counter - 1],
                    City = googleLocation[Counter - 3],
                    State = googleLocation[Counter - 2],
                    InsertDate = DateTime.UtcNow
                };
                _context.Locations.Add(location);
                //_context.SaveChanges();,
            }

            kheechEvent.LocationId = location.Id;

            _context.KheechEvents.Add(kheechEvent);
           
            foreach (var kuser in model.WhoToMeet)
            {
                var kheechUser = new KheechUser
                {
                    KheechEventId = kheechEvent.Id,
                    ApplicationUserId = kuser,
                    IsAccepted = false
                };

                _context.KheechUsers.Add(kheechUser);
            }
            await _context.SaveChangesAsync();

            TempData["ScheduleMessage"] = "Congratulations, you have successfully added a Kheech. Keep going!";
            return RedirectToRoute("SendKheechNotification", new { id = kheechEvent.Id, returnFrom = "Schedule"});
        }

        // GET: KheechEvents/Edit/5
        [Route("Edit/{id}", Name = "KheechEdit")]
        public async Task<ActionResult> Edit(int? id)
        {
            var currentUserId = User.Identity.GetUserId();

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var kheechEditViewModels = new KheechEditViewModels();

            kheechEditViewModels.KheechEvent = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                           .Include(k => k.KheechComments)
                                                           .Include(k => k.KheechUsers)
                                                           .Include(k => k.Location)
                                                           .FirstOrDefaultAsync(k => k.Id == id);
            if (kheechEditViewModels.KheechEvent == null)
            {
                return HttpNotFound();
            }

            var friends = await _context.Friendships
                                  .Include(f => f.Initiator)
                                  .Include(f => f.Recipient)
                                  .Include(f => f.FriendshipStatus)
                                  .Where(f => f.InitiatorId == currentUserId || f.RecipientId == currentUserId).Distinct().ToListAsync();

            foreach (var item in friends)
            {
                if (item.RecipientId == currentUserId)
                {
                    var friendViewModel1 = new FriendViewModel
                    {
                        Id = item.InitiatorId,
                        Name = item.Initiator.FirstName
                    };
                    kheechEditViewModels.Friends.Add(friendViewModel1);
                }
                else
                {
                    var friendViewModel = new FriendViewModel
                    {
                        Id = item.RecipientId,
                        Name = item.Recipient.FirstName
                    };
                    kheechEditViewModels.Friends.Add(friendViewModel);
                }
            }

            kheechEditViewModels.WhereToMeet = kheechEditViewModels.KheechEvent.Location.Name;
            //ViewBag.ApplicationUserId = new SelectList(db.ApplicationUsers, "Id", "FirstName", kheechEvent.ApplicationUserId);
            ViewBag.GroupId = new SelectList(_context.Groups, "Id", "Name", kheechEditViewModels.KheechEvent.GroupId);
            //ViewBag.LocationId = new SelectList(_context.Locations, "Id", "Name", kheechEditViewModels.KheechEvent.LocationId);

            return View(kheechEditViewModels);
        }

        // POST: KheechEvents/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Edit/{id}", Name = "KheechEditPost")]
        public async Task<ActionResult> Edit(int id, KheechEditViewModels kheechEditViewModels)
        {
            if (ModelState.IsValid)
            {
                var kheechEventOld = await _context.KheechEvents.Include(k => k.Location).FirstOrDefaultAsync(k => k.Id == kheechEditViewModels.KheechEvent.Id);
                bool isKheechChanged = false;

                if ((kheechEventOld.StartDate != kheechEditViewModels.KheechEvent.StartDate) || (kheechEventOld.Location.Name != kheechEditViewModels.WhereToMeet))
                {
                    kheechEventOld.StartDate = kheechEditViewModels.KheechEvent.StartDate;
                    kheechEventOld.EndDate = kheechEditViewModels.KheechEvent.StartDate.AddHours(2);
                    kheechEventOld.InsertDate = DateTime.UtcNow;

                    var googleLocation = kheechEditViewModels.WhereToMeet.Split(',').ToList();
                    var locationToMeet = googleLocation[0];
                    var Counter = googleLocation.Count;

                    var location = await _context.Locations.FirstOrDefaultAsync(l => l.Name == locationToMeet);
                    if (location == null)
                    {
                        location = new Location
                        {
                            Name = locationToMeet,
                            address1 = googleLocation[Counter - 4],
                            Country = googleLocation[Counter - 1],
                            City = googleLocation[Counter - 3],
                            State = googleLocation[Counter - 2],
                            InsertDate = DateTime.UtcNow
                        };
                        _context.Locations.Add(location);
                        await _context.SaveChangesAsync();
                    }

                    kheechEventOld.LocationId = location.Id;
                    isKheechChanged = true;
                }

                foreach (var kuser in kheechEditViewModels.WhoToMeet)
                {
                    var existingKheechUser = await _context.KheechUsers.FirstOrDefaultAsync(k => k.KheechEventId == id && k.ApplicationUserId == kuser);

                    if (existingKheechUser == null)
                    {
                        var kheechUser = new KheechUser
                        {
                            KheechEventId = kheechEditViewModels.KheechEvent.Id,
                            ApplicationUserId = kuser,
                            IsAccepted = false
                        };

                        _context.KheechUsers.Add(kheechUser);
                    }
                }

                if (isKheechChanged)
                {
                    var oldKheechUsers = await _context.KheechUsers.Where(k => k.KheechEventId == kheechEditViewModels.KheechEvent.Id).ToListAsync();

                    foreach (var kuser in oldKheechUsers)
                    {
                        kuser.IsAccepted = false;
                        _context.Entry(kuser).State = EntityState.Modified;
                    }
                }

                kheechEventOld.EventName = kheechEditViewModels.KheechEvent.EventName;
                _context.Entry(kheechEventOld).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return RedirectToRoute("SendKheechNotification", new { id = kheechEventOld.Id, returnFrom = "Edit" });
            }
            //ViewBag.ApplicationUserId = new SelectList(db.ApplicationUsers, "Id", "FirstName", kheechEvent.ApplicationUserId);
            ViewBag.GroupId = new SelectList(_context.Groups, "Id", "Name", kheechEditViewModels.KheechEvent.GroupId);
            ViewBag.LocationId = new SelectList(_context.Locations, "Id", "Name", kheechEditViewModels.KheechEvent.LocationId);
            return View(kheechEditViewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AddDiscussion/{id}", Name = "AddDiscussion")]
        public async Task<ActionResult> AddDiscussion(int id, string newMessage)
        {
            var currentUserId = User.Identity.GetUserId();
            var kheechComment = new KheechComment
            {
                Discussion = newMessage,
                InsertDate = DateTime.UtcNow,
                KheechEventId = id,
                CreatorId = currentUserId
            };

            _context.KheechComments.Add(kheechComment);
            await _context.SaveChangesAsync();

            return RedirectToRoute("KheechDetails", new { id = id });
        }

        [HttpPost]
        [Route("DeleteDiscussion", Name = "DeleteDiscussion")]
        public async Task<ActionResult> DeleteDiscussion(int id)
        {
            var commentToBeDeleted = await _context.KheechComments.FirstOrDefaultAsync(c => c.Id == id);
            var kheechId = commentToBeDeleted.KheechEventId;
            if (commentToBeDeleted == null)
            {
                return HttpNotFound();
            }

            _context.KheechComments.Remove(commentToBeDeleted);
            await _context.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AcceptedKheech/{id}", Name = "AcceptedKheechPost")]
        public async Task<ActionResult> AcceptedKheech(int id, bool isAccepted)
        {
            var currentUserId = User.Identity.GetUserId();
            var kheechUser = await _context.KheechUsers.FirstOrDefaultAsync(k => k.KheechEventId == id && k.ApplicationUserId == currentUserId);

            if (kheechUser == null)
            {
                return HttpNotFound();
            }

            kheechUser.IsAccepted = isAccepted;
            await _context.SaveChangesAsync();

            return RedirectToRoute("KheechDetails", new { id = id });
        }
        
        [Route("SendKheechNotification", Name = "SendKheechNotification")]
        public async Task<ActionResult> SendKheechNotification(int id, string returnFrom)
        {
            var kheechEvent = await _context.KheechEvents.Include(k => k.ApplicationUser)
                                                         .Include(k => k.Location)
                                                         .FirstOrDefaultAsync(k => k.Id == id);

            var kheechUsers = await _context.KheechUsers.Include(k => k.ApplicationUser)
                                                        .Include(k => k.KheechEvent)
                                                        .Where(k => k.KheechEventId == id)
                                                        .ToListAsync();

            var subject = "Your Friends have invited you to a kheech Event";
            var message = $"{kheechEvent.ApplicationUser.FirstName.Humanize()} {kheechEvent.ApplicationUser.LastName.Humanize()} would like you to join her and friends over " +
                          $"{kheechEvent.EventName.Humanize()} at {kheechEvent.Location.Name.Humanize()} on {kheechEvent.StartDate.ToOrdinalWords()}. " +
                          $"Logon to the app for more details and accept the event invite.";
            var sendGridClient = new SendGridEmailClient(ConfigurationManager.AppSettings["SendGridApiKey"]);

            foreach (var kuser in kheechUsers)
            {
                await sendGridClient.SendEmailAsync(kuser.ApplicationUser.Email, subject, message);
            }

            if (returnFrom == "Schedule")
            {
                return RedirectToRoute("HomePage");
            }
            else
            {
                return RedirectToRoute("KheechDetails", new { id = id });
            }
        }
    }
}
