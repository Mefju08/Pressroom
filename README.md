# Pressroom

Projekt Pressroom to platforma mikroserwisowa do zgłaszania i recenzowania artykułów. Komunikacja między serwisami odbywa się za pomocą protokołu gRPC, a dostęp dla użytkowników zewnętrznych zapewnia bramka REST API.

## Struktura projektów

- **Pressroom.Gateway**: Punkt wejścia do systemu. Udostępnia interfejs REST API i tłumaczy zapytania HTTP na wywołania gRPC do usług backendowych. Obsługuje błędy gRPC i zwraca odpowiednie kody statusu HTTP (400, 401, 403, 404, 500).
- **Pressroom.Editorial**: Serwis redakcyjny odpowiedzialny za proces zgłaszania artykułów. Zarządza przepływem pracy (workflow) artykułu, komunikując się z serwisem Review.
- **Pressroom.Review**: Serwis recenzji, który przechowuje historię i statusy artykułów w pamięci (symulacja bazy danych). Symuluje proces oceny artykułu przez recenzenta, w tym prośby o poprawki oraz ostateczne zatwierdzenie lub odrzucenie.
- **Pressroom.Contracts**: Biblioteka współdzielona zawierająca definicje Protobuf (`.proto`) oraz wygenerowany kod gRPC używany przez wszystkie serwisy.

## Endpointy i przepływ żądań

### 1. POST `/articles` (Zgłoszenie artykułu)
- **Co się dzieje:**
    1. Gateway odbiera dane artykułu (tytuł, treść, ID autora) przez REST.
    2. Gateway wywołuje usługę `Editorial` przez gRPC (`SubmitArticle`).
    3. `Editorial` generuje unikalne `ArticleId` i otwiera dwukierunkowy strumień z usługą `Review`.
    4. `Review` zapisuje artykuł i symuluje proces recenzji, przesyłając kolejne statusy (`Pending` -> `InReview` -> `ChangesRequested`).
    5. Gdy `Review` zgłasza potrzebę poprawek (`ChangesRequested`), `Editorial` automatycznie modyfikuje treść artykułu (dodając dopisek "[revised]") i przesyła go ponownie do `Review`.
    6. `Review` losowo podejmuje ostateczną decyzję: `Approved` (zatwierdzony) lub `Rejected` (odrzucony).
    7. Po zakończeniu procesu `Editorial` zwraca do Gateway identyfikator artykułu i czas zgłoszenia.
- **Wynik:** Użytkownik otrzymuje ID utworzonego artykułu i potwierdzenie wysłania.

### 2. GET `/articles/{id}/status` (Pobranie aktualnego statusu)
- **Co się dzieje:**
    1. Gateway odbiera ID artykułu.
    2. Gateway pyta usługę `Editorial` o status artykułu.
    3. `Editorial` pobiera z usługi `Review` ostatni (najnowszy) wpis w historii recenzji dla danego artykułu.
    4. Usługa `Review` wyszukuje artykuł w swojej pamięci i zwraca jego bieżący stan.
- **Wynik:** Użytkownik otrzymuje informację o aktualnym statusie (np. `Approved`, `InReview`), ewentualny komentarz recenzenta oraz czas ostatniej aktualizacji.

### 3. GET `/articles/{id}/history` (Pobranie pełnej historii recenzji)
- **Co się dzieje:**
    1. Gateway odbiera ID artykułu.
    2. Gateway wywołuje strumieniowy endpoint w usłudze `Editorial`.
    3. `Editorial` łączy się ze strumieniem w usłudze `Review`.
    4. `Review` przesyła po kolei wszystkie historyczne statusy artykułu (z opóźnieniem 1 sekundy między wpisami dla symulacji).
    5. Gateway zbiera wszystkie elementy ze strumienia i po jego zakończeniu zwraca pełną listę historii jako JSON.
- **Wynik:** Użytkownik otrzymuje listę wszystkich etapów, przez które przeszedł artykuł podczas procesu recenzji.
