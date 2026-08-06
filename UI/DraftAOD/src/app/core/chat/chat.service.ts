import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ChatPlayerResult { id: string; name: string; rank: number; position: string; goalCredit: number; assistCredit: number; defensiveCredit: number; }
export interface ChatResponse { intent: string; answer: string; noDataFound: boolean; players: ChatPlayerResult[]; }

@Injectable({ providedIn: 'root' })
export class ChatService {
  constructor(private readonly http: HttpClient) {}
  query(message: string) { return this.http.post<ChatResponse>(`${environment.apiUrl}/chatbot/query`, { message }); }
}
