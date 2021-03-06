﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Evernote.EDAM.UserStore;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using MoneyPortal.Models;
using MoneyPortal.Classes;
using System.Threading.Tasks;
using CsQuery.ExtensionMethods.Internal;
using System.Web.ModelBinding;
using CsQuery.ExtensionMethods;
using CsQuery.Utility;
using Newtonsoft.Json;
using MoneyPortal.ViewModels;

namespace MoneyPortal.Controllers
{
    [Authorize, RoutePrefix("MyHome")]
    public class HouseholdsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private UserManager<ApplicationUser> userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext()));

        //GET: Households/Transactions
        [Authorize(Roles ="Owner, Member")]
        public ActionResult Transactions()
        {
            var householdId = db.Users.Find(User.Identity.GetUserId()).HouseholdId;
            var viewData = new HouseholdTransactionsVM
            {
                HouseholdName = db.Households.Find(householdId).Name,
                Transactions = new List<TransactionVM>()
            };
            foreach (var transaction in db.Transactions.Where(t => t.BankAccount.HouseholdId == householdId)
                                                        .Include(t => t.BankAccount)
                                                        .Include(t => t.TransactionType)
                                                        .Where(t => t.TransactionType.Name != "Deposit" && t.TransactionType.Name != "Transfer")
                                                        .Include(t => t.CategoryItem)
                                                        .Include(t => t.BankAccount.Owner).ToList())
            {
                var tVM = new TransactionVM
                {
                    Id = transaction.Id,
                    BankAccountName = transaction.BankAccount.Name,
                    Owner = transaction.BankAccount.Owner,
                    Amount = transaction.Amount,
                    Type = transaction.TransactionType.Name,
                    Memo = transaction.Memo,
                    BudgetItem = (transaction.CategoryItem != null ? transaction.CategoryItem.Name : ""),
                    Date = transaction.Created    
                };
                //if(transaction.CategoryItem != null)
                //{
                //    tVM.BudgetItem = transaction.CategoryItem.Name;
                //}

                var items = new List<SelectListItem>();
                foreach(var budget in db.Categories.Where(c => c.HouseholdId == householdId).OrderBy(c => c.Name).ToList())
                {
                    var group = new SelectListGroup() { Name = budget.Name };
                    foreach(var budgetItem in budget.CategoryItems.OrderBy(ci => ci.Name).ToList())
                    {
                        items.Add(new SelectListItem() { 
                            Value = budgetItem.Id.ToString(), 
                            Text = budgetItem.Name, 
                            Group = group
                        });
                    }
                }
                items.Add(new SelectListItem()
                {
                    Value = null,
                    Text = "Remove Budget",
                    Group = new SelectListGroup() { Name = "- Remove -"}
                });
                
                tVM.BudgetItemList = new SelectList(items, "Value", "Text", "Group.Name", transaction.CategoryItemId);

                viewData.Transactions.Add(tVM);
            }
            return View(viewData);
        }
        //GET: Households/BankAccounts
        [Authorize(Roles ="Owner, Member")]
        public ActionResult BankAccounts ()
        {
            var householdId = db.Users.Find(User.Identity.GetUserId()).HouseholdId;
            return View(db.BankAccounts.Where(ba => ba.HouseholdId == householdId).Include(ba => ba.Type).Include(ba => ba.Owner));
        }
        //GET: Households/Members
        [Authorize(Roles = "Owner, Member")]
        public ActionResult Members()
        {
            var householdId = db.Users.Find(User.Identity.GetUserId()).HouseholdId;
            var owner = db.Users.Find(db.Households.Find(householdId).OwnerId);

            var viewData = new HouseholdMembersVM
            {
                Owner = new HouseholdMember
                {
                    Id = owner.Id,
                    FullName = owner.FullName,
                    Avatar = owner.AvatarPath,
                    JoinedAccounts = owner.BankAccounts.Where(ba => ba.HouseholdId == householdId).Count(),
                    Transactions = owner.BankAccounts.Where(ba => ba.HouseholdId == householdId).SelectMany(ba => ba.Transactions).Count()
                }
            };
            var Users = db.Users.Where(u => u.HouseholdId == householdId).ToList();
            foreach (var user in Users)
            {
                if (user.Id != db.Households.Find(householdId).OwnerId)
                {
                    viewData.Members.Add(new HouseholdMember
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Avatar = user.AvatarPath,
                        JoinedAccounts = user.BankAccounts.Where(ba => ba.HouseholdId == householdId).Count(),
                        Transactions = user.BankAccounts.Where(ba => ba.HouseholdId == householdId).SelectMany(ba => ba.Transactions).Count()
                    });
                }
            }

            return View(viewData);
        }

        // POST: Households/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string HouseholdName, string HouseholdGreeting)
        {

            if (!HouseholdName.IsNullOrEmpty() && !HouseholdGreeting.IsNullOrEmpty())
            {
                var userId = User.Identity.GetUserId();
                var user = db.Users.Find(User.Identity.GetUserId());

                Household household = new Household
                {
                    Created = DateTime.Now,
                    Name = HouseholdName,
                    Greeting = HouseholdGreeting,
                    OwnerId = userId
                };
                db.Households.Add(household);
                db.SaveChanges();
                user.HouseholdId = household.Id;
                db.SaveChanges();

                userManager.RemoveFromRole(userId, "Personal");
                userManager.AddToRole(userId, "Owner");

                await UserAuthorization.RefreshAuthentication(HttpContext, user);

                return RedirectToAction("Main", "Dashboard");
            }
            else
            {
                return RedirectToAction("Main", "Dashboard", new { ErrorMessage = "Your input was incomplete." });
            }

        }

        //POST: Households/JoinBankAccount
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Owner, Member")]
        public JsonResult JoinBankAccount(int UsersBankAccounts, int HouseholdId)
        {
            try
            {
                var userId = User.Identity.GetUserId();
                var account = db.BankAccounts.Find(UsersBankAccounts);
                account.HouseholdId = HouseholdId;
                db.Entry(account).State = EntityState.Modified;
                db.SaveChanges();

                var remainingAccounts = db.BankAccounts.Where(ba => ba.OwnerId == userId && ba.HouseholdId == null).ToList();
                var data = new Dictionary<int, string>();
                foreach (var a in remainingAccounts)
                {
                    data.Add(a.Id, a.DisplayName);
                }

                return Json(JsonConvert.SerializeObject(data));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Json(false);
            }
        }

        //GET: Households/RemoveBankAccount
        [Authorize(Roles ="Owner, Member")]
        public JsonResult RemoveBankAccount(int id)
        {
            try
            {
                var account = db.BankAccounts.Find(id);
                if(User.IsInRole("Owner") || account.OwnerId == User.Identity.GetUserId())
                {
                    db.BankAccounts.Find(id).HouseholdId = null;
                    db.Transactions.Where(t => t.BankAccountId == id).ForEach(t => t.CategoryItemId = null);
                    db.SaveChanges();
                    return Json(true, JsonRequestBehavior.AllowGet);
                } else
                {
                    Console.WriteLine("Can't Remove another users account!");
                    return Json(false);
                }
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(false);
            }
        }

        //POST: Households/LinkBankAccount
        [HttpPost, Authorize(Roles ="Owner, Member")]
        public JsonResult LinkBankAccount(int id)
        {
            try
            {
                var userId = User.Identity.GetUserId();
                var householdId = db.Users.Find(userId).HouseholdId;
                var account = db.BankAccounts.Find(id);
                
                account.HouseholdId = householdId;
                db.SaveChanges();

                return Json(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(false);
            }
        }

        //POST: Households/LinkBankAccount
        [HttpPost, Authorize(Roles ="Owner, Member")]
        public JsonResult LinkBankAccount(int UsersBankAccounts, int HouseholdId)
        {
            try
            {
                var userId = User.Identity.GetUserId();
                var account = db.BankAccounts.Find(UsersBankAccounts);
                
                account.HouseholdId = HouseholdId;
                db.SaveChanges();

                return Json(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(false);
            }
        }


        //POST: Households/RemoveMember
        [HttpPost, Authorize(Roles = "Owner")]
        public JsonResult RemoveMember(string id)
        {
            try
            {
                var user = db.Users.Find(id);
                user.HouseholdId = null;
                db.BankAccounts.Where(ba => ba.OwnerId == user.Id).ForEach(ba => ba.HouseholdId = null);
                db.BankAccounts.Where(ba => ba.OwnerId == user.Id).SelectMany(ba => ba.Transactions).ForEach(t => t.CategoryItemId = null);
                db.SaveChanges();

                userManager.RemoveFromRole(user.Id, "Member");
                userManager.AddToRole(user.Id, "Personal");

                return Json(true, JsonRequestBehavior.AllowGet);
            } catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Households/Delete
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult> Delete()
        {
            var householdId = db.Users.Find(User.Identity.GetUserId()).HouseholdId;
            var users = db.Users.Where(u => u.HouseholdId == householdId).ToList();

            foreach (var user in users)
            {
                user.HouseholdId = null;
                db.BankAccounts.Where(ba => ba.OwnerId == user.Id && ba.HouseholdId == householdId).SelectMany(ba => ba.Transactions).ForEach(t => t.CategoryItemId = null);
                db.BankAccounts.Where(ba => ba.OwnerId == user.Id && ba.HouseholdId == householdId).ForEach(ba => ba.HouseholdId = null);

                if (User.IsInRole("Owner"))
                    userManager.RemoveFromRole(user.Id, "Owner");
                else
                    userManager.RemoveFromRole(user.Id, "Member");

                userManager.AddToRole(user.Id, "Personal");
            }

            foreach (var budget in db.Categories.Where(c => c.HouseholdId == householdId).ToList())
            {
                foreach (var budgetItem in budget.CategoryItems.ToList())
                {
                    db.CategoryItems.Remove(budgetItem);
                }
                db.Categories.Remove(budget);
            }

            db.Households.Remove(db.Households.Find(householdId));
            db.SaveChanges();

            await UserAuthorization.RefreshAuthentication(HttpContext, db.Users.Find(User.Identity.GetUserId()));

            return RedirectToAction("Main", "Dashboard");
        }

        //GET: Households/Leave
        [Authorize(Roles = "Member")]
        public async Task<ActionResult> Leave()
        {
            var user = db.Users.Find(User.Identity.GetUserId());
            user.HouseholdId = null;
            db.BankAccounts.Where(ba => ba.OwnerId == user.Id).ForEach(ba => ba.HouseholdId = null);
            db.BankAccounts.Where(ba => ba.OwnerId == user.Id).SelectMany(ba => ba.Transactions).ForEach(t => t.CategoryItemId = null);
            db.SaveChanges();

            userManager.RemoveFromRole(user.Id, "Member");
            userManager.AddToRole(user.Id, "Personal");
            await UserAuthorization.RefreshAuthentication(HttpContext, user);

            return RedirectToAction("Main", "Dashboard");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}