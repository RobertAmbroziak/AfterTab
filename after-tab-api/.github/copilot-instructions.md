- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

## AfterTab participant and settlement rules

- Keep the root `README.md` as the user/developer manual; update its local startup and usage instructions when behavior changes.
- A user account and an event participant are separate identities. `EventParticipant.UserId` is optional. Never create fictional `Users` or sessions for participants without accounts.
- Splits, payers and transfer confirmations use event participant IDs. `AuthorUserId` remains the authenticated account that entered the expense. Validate all financial references within the same event.
- Only the event owner may manage virtual participants, and only while the event is open. Never match people automatically by display name.
- Linking preserves the virtual participant's ID, combines duplicate expense shares, moves payer references and retains authorship and historical private payment flags. Perform the entire operation in a serializable transaction and update affected expense rowversions.
- Only the authenticated sender or recipient may change their own payment flag. An event owner cannot confirm on behalf of a virtual participant. Historical aliases must not expose flags to unrelated members.
- Keep migrations data-preserving. Refuse rollback that would discard virtual participants or merged aliases. Test migrations against a separate local database before touching real data.
- Do not deploy, commit or push unless explicitly requested. Do not include secrets or generated test/deployment artifacts in Git.

- Expense balancing is explicit: only the event owner or current payer may assign the remainder to a selected split participant. Never fall back to the largest share or item author. Persist both `BalanceParticipantId` and `BalanceAmountMinor`, undo the previous adjustment before moving it, and preserve base amounts and percentage inputs. Keep this permission on the server as well as in the UI. Linking participants must also remap the balance target. Legacy balanced items have no inferred target or correction amount.
