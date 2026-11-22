# Member D — AI Service, Chatbot & Notifications

## Your Ownership
- **Microservices:** `backend/src/AiService/`, `backend/src/NotificationService/`
- **Backend Controllers & Workers:** `AssistantController.cs`, `NotificationsController.cs`, `AlertsController.cs`, `NotificationDispatcher.cs`
- **External Integrations:** `AiClient.cs`, `AiAssistantClient.cs`, `LocalFaq.cs`, `Senders.cs`
- **Frontend Components:** `client/src/components/ChatBot.jsx`, `client/src/components/Notifier.jsx`, `client/src/components/AlertBar.jsx`

## Key Tasks (from WORK-PLAN.md)
1. Chatbot memory: send the last few messages (`AssistantController` + `AiService /assist` + `ChatBot.jsx`), preserving 500-char & prompt-injection limits.
2. Unit tests: keyword classification, LLM output validation (category/urgency clamped), notification retry and dead-letter.
3. Review Sinhala & Tamil templates and FAQ answers (`LocalFaq.cs`, `Senders.cs`).
4. Document SMTP / `ANTHROPIC_API_KEY` setup & write AI services / resilience chapter.
