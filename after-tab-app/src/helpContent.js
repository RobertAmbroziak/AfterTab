export const helpSections = [
  {
    id: 'login',
    title: { pl: 'Logowanie', en: 'Sign in' },
    items: [
      { title: { pl: 'Zacznij jako gość', en: 'Start as a guest' }, description: { pl: 'Możesz korzystać z AfterTab jako gość.', en: 'You can use AfterTab as a guest.' }, images: ['L01'] },
      { title: { pl: 'Podaj imię', en: 'Enter your name' }, description: { pl: 'Twoje imię zostanie zapamiętane na tym urządzeniu i w tej przeglądarce. Działania wykonane jako gość będą z nim powiązane.', en: 'Your name is remembered on this device and in this browser. Actions you take as a guest are linked to it.' }, images: ['L02'] },
      { title: { pl: 'Zarządzaj sesją gościa', en: 'Manage your guest session' }, description: { pl: 'W każdej chwili możesz zmienić imię, usunąć zapamiętanego gościa, wylogować się lub połączyć gościa ze stałym kontem.', en: 'At any time, you can change your name, remove the remembered guest, sign out, or link the guest to a permanent account.' }, images: ['L03'] },
      { title: { pl: 'Wróć do swojego gościa', en: 'Return to your guest' }, description: { pl: 'Po wylogowaniu możesz zalogować się na inne konto albo wrócić jako ten sam gość. Jego imię i działania znów będą dostępne.', en: 'After signing out, you can use another account or return as the same guest. Their name and activity will be available again.' }, images: ['L04'] },
      { title: { pl: 'Połącz gościa z kontem', en: 'Link a guest to an account' }, description: { pl: 'Połącz gościa z Facebookiem, Google lub istniejącym kontem e-mail. Możesz też utworzyć nowe konto.', en: 'Link your guest to Facebook, Google, or an existing email account. You can also create a new account.' }, images: ['L05'] },
      { title: { pl: 'Zaloguj się lub utwórz konto', en: 'Sign in or create an account' }, description: { pl: 'Bez zalogowania możesz użyć Facebooka, Google lub e-maila, utworzyć konto, przypomnieć hasło albo ponownie wysłać e-mail potwierdzający rejestrację.', en: 'If you are signed out, use Facebook, Google, or email; create an account; reset your password; or resend the registration confirmation email.' }, images: ['L06'] },
      { title: { pl: 'Połącz uczestników', en: 'Link participants' }, description: { pl: 'Połącz uczestnika wirtualnego z prawdziwym, jeśli to ta sama osoba. Użytkownik z kontem może też sam połączyć się z uczestnikiem wirtualnym.', en: 'Link a virtual participant to a real participant if they are the same person. An account holder can also link themselves to a virtual participant.' }, images: ['L07', 'L08'] },
    ],
  },
  {
    id: 'events',
    title: { pl: 'Wydarzenia', en: 'Events' },
    items: [
      { title: { pl: 'Utwórz wydarzenie', en: 'Create an event' }, description: { pl: 'Nowe wydarzenie utworzysz po zalogowaniu — również jako gość.', en: 'You can create an event after signing in, including as a guest.' }, images: ['E01'] },
      { title: { pl: 'Ustaw nazwę i walutę', en: 'Set a name and currency' }, description: { pl: 'Podaj nazwę wydarzenia, wybierz walutę (domyślnie PLN) i, jeśli chcesz, dodaj opis.', en: 'Enter an event name, choose a currency (PLN by default), and add an optional description.' }, images: ['E02'] },
      { title: { pl: 'Edytuj wydarzenie i uczestników', en: 'Edit the event and participants' }, description: { pl: 'Możesz zmienić nazwę wydarzenia lub je usunąć. W sekcji uczestników zarządzisz pozostałymi osobami.', en: 'You can rename or delete the event. Manage the other people in the participants section.' }, images: ['E03'] },
      { title: { pl: 'Dodaj wydatek', en: 'Add an expense' }, description: { pl: 'Wybierz osoby, które mają uczestniczyć w wydatku, wpisz jego nazwę i kwotę, a następnie wskaż płacącego.', en: 'Choose who is included in the expense, enter its name and amount, then select who paid.' }, images: ['E04', 'E07'] },
      { title: { pl: 'Udostępnij wydarzenie', en: 'Share the event' }, description: { pl: 'Utwórz link i udostępnij go na przykład przez komunikator, e-mail lub kod QR. Możesz unieważnić link i wygenerować nowy.', en: 'Create a link and share it through a messaging app, email, or QR code. You can revoke the link and create a new one.' }, images: ['E05'] },
      { title: { pl: 'Dodaj uczestników wirtualnych', en: 'Add virtual participants' }, description: { pl: 'Dodaj osoby, które nie dołączają do wydarzenia przez aplikację. Możesz uwzględniać je w wydatkach.', en: 'Add people who are not joining the event through the app. You can include them in expenses.' }, images: ['E06'] },
      { title: { pl: 'Podziel wydatek', en: 'Split an expense' }, description: { pl: 'Podziel kwotę równo, ustaw udziały procentowe lub wpisz konkretne kwoty. Na koniec potwierdź dodanie wydatku.', en: 'Split the amount equally, set percentage shares, or enter exact amounts. Confirm to add the expense.' }, images: ['E08'] },
      { title: { pl: 'Rozdziel pozostałe grosze', en: 'Assign the remaining cents' }, description: { pl: 'Jeśli podział nie daje pełnych groszy, wskaż osobę, do której trafi pozostała kwota.', en: 'If the split leaves fractions of a cent, choose who receives the remaining amount.' }, images: ['E09'] },
      { title: { pl: 'Dodawaj kolejne wydatki', en: 'Add more expenses' }, description: { pl: 'Możesz dodać wiele wydatków, a pozostali uczestnicy również mogą je dodawać i dzielić. Wydatki wpływają na kwotę do otrzymania lub oddania. Wydatku z innym płacącym niż Ty może dodać tylko autor wydarzenia.', en: 'Add as many expenses as you need. Other participants can add and split them too. Expenses affect how much you owe or are owed. Only the event owner can add an expense paid for by someone else.' }, images: ['E10'] },
    ],
  },
  {
    id: 'settlements',
    title: { pl: 'Rozliczenia', en: 'Settlements' },
    items: [
      { title: { pl: 'Zamknij wydarzenie', en: 'Close the event' }, description: { pl: 'Po zamknięciu wydarzenia zobaczysz, kto komu i ile ma oddać.', en: 'Close the event to see who owes money to whom, and how much.' }, images: ['R01'] },
      { title: { pl: 'Sprawdź bilans uczestników', en: 'Check participant balances' }, description: { pl: 'W bilansie znajdziesz podsumowanie każdej osoby.', en: 'The balance shows a summary for each person.' }, images: ['R02'] },
      { title: { pl: 'Oznacz przelewy', en: 'Mark transfers' }, description: { pl: 'Zaznacz wysłane lub otrzymane kwoty, aby uwzględnić je w rozliczeniu.', en: 'Mark amounts as sent or received to update the settlement.' }, images: ['R03'] },
      { title: { pl: 'Przydziel kwoty przed zamknięciem', en: 'Assign all amounts before closing' }, description: { pl: 'Nie zamkniesz wydarzenia, dopóki jakieś kwoty pozostają nieprzydzielone. Po zamknięciu nie można dodawać nowych danych — chyba że ponownie otworzysz wydarzenie.', en: 'You cannot close the event while any amounts are unassigned. Once closed, no one can add new information unless you reopen it.' }, images: ['R04', 'R05'] },
    ],
  },
]
