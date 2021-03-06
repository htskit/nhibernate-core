﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AsyncGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------


using System.Linq;
using NHibernate.Engine;
using NHibernate.Persister.Entity;
using NUnit.Framework;
using NHibernate.Linq;

namespace NHibernate.Test.NHSpecificTest.GH1226
{
	using System.Threading.Tasks;
	[TestFixture]
	public class FixtureAsync : BugTestCase
	{
		protected override void OnSetUp()
		{
			base.OnSetUp();

			using (var session = OpenSession())
			{
				using (var tx = session.BeginTransaction())
				{
					var bank = new Bank { Code = "01234" };
					session.Save(bank);

					var account = new Account { Bank = bank };
					session.Save(account);

					var account2 = new Account { Bank = bank };
					session.Save(account2);

					tx.Commit();
				}
			}
			Sfi.Statistics.IsStatisticsEnabled = true;
		}

		[Test]
		public async Task BankShouldBeJoinFetchedAsync()
		{
			// Simple case: nothing already in session.
			using (var session = OpenSession())
			using (var tx = session.BeginTransaction())
			{
				var countBeforeQuery = Sfi.Statistics.PrepareStatementCount;

				var accounts = await (session.CreateQuery("from Account a left join fetch a.Bank").ListAsync<Account>());
				var associatedBanks = accounts.Select(x => x.Bank).ToList();
				Assert.That(associatedBanks, Has.All.Matches<object>(NHibernateUtil.IsInitialized),
				            "One bank or more was lazily loaded.");

				var countAfterQuery = Sfi.Statistics.PrepareStatementCount;
				var statementCount = countAfterQuery - countBeforeQuery;

				await (tx.CommitAsync());

				Assert.That(statementCount, Is.EqualTo(1));
			}
		}

		[Test]
		public async Task InSessionBankShouldBeJoinFetchedAsync()
		{
			using (var session = OpenSession())
			using (var tx = session.BeginTransaction())
			{
				// #1226 bug only occurs if the Banks are already in the session cache.
				await (session.CreateQuery("from Bank").ListAsync<Bank>());

				var countBeforeQuery = Sfi.Statistics.PrepareStatementCount;

				var accounts = await (session.CreateQuery("from Account a left join fetch a.Bank").ListAsync<Account>());
				var associatedBanks = accounts.Select(x => x.Bank).ToList();
				Assert.That(associatedBanks, Has.All.Matches<object>(NHibernateUtil.IsInitialized),
				            "One bank or more was lazily loaded.");

				var countAfterQuery = Sfi.Statistics.PrepareStatementCount;
				var statementCount = countAfterQuery - countBeforeQuery;

				await (tx.CommitAsync());

				Assert.That(statementCount, Is.EqualTo(1));
			}
		}

		[Test]
		public async Task AlteredBankShouldBeJoinFetchedAsync()
		{
			using (var s1 = OpenSession())
			{
				using (var tx = s1.BeginTransaction())
				{
					// Put them all in s1 cache.
					await (s1.CreateQuery("from Bank").ListAsync());
					await (tx.CommitAsync());
				}

				string oldCode;
				const string newCode = "12345";
				// Alter the bank code with another session.
				using (var s2 = OpenSession())
				using (var tx2 = s2.BeginTransaction())
				{
					var accounts = await (s2.Query<Account>().ToListAsync());
					foreach (var account in accounts)
						account.Bank = null;
					await (s2.FlushAsync());
					var bank = await (s2.Query<Bank>().SingleAsync());
					oldCode = bank.Code;
					bank.Code = newCode;
					await (s2.FlushAsync());
					foreach (var account in accounts)
						account.Bank = bank;
					await (tx2.CommitAsync());
				}

				// Check querying them with s1 is still consistent
				using (var tx = s1.BeginTransaction())
				{
					var accounts = await (s1.CreateQuery("from Account a left join fetch a.Bank").ListAsync<Account>());
					var associatedBanks = accounts.Select(x => x.Bank).ToList();
					Assert.That(associatedBanks, Has.All.Not.Null,
					            "One bank or more failed loading.");
					Assert.That(associatedBanks, Has.All.Matches<object>(NHibernateUtil.IsInitialized),
					            "One bank or more was lazily loaded.");
					Assert.That(associatedBanks, Has.All.Property(nameof(Bank.Code)).EqualTo(oldCode),
					            "One bank or more has no more the old code.");

					await (tx.CommitAsync());
					// Do not check statements count: we are in a special case defeating the eager fetching, because
					// we have stale data in session for the bank code.
					// But check that the new code, supposed to be unknown for the session, is not cached.
					var persister = Sfi.GetEntityPersister(typeof(Bank).FullName);
					var index = ((IUniqueKeyLoadable) persister).GetPropertyIndex(nameof(Bank.Code));
					var type = persister.PropertyTypes[index];
					var euk = new EntityUniqueKey(persister.EntityName, nameof(Bank.Code), newCode, type, Sfi);
					Assert.That(s1.GetSessionImplementation().PersistenceContext.GetEntity(euk),
						Is.Null, "Found a bank associated to the new code in s1");
				}
			}
		}

		protected override void OnTearDown()
		{
			base.OnTearDown();

			using (var session = OpenSession())
			using (var tx = session.BeginTransaction())
			{
				session.CreateQuery("delete from Account").ExecuteUpdate();
				session.CreateQuery("delete from Bank").ExecuteUpdate();
				tx.Commit();
			}
		}
	}
}
