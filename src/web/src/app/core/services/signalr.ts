import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { HttpTransportType } from '@microsoft/signalr';
import { getStoredAccessToken } from '../auth/auth-session';
import { API_BASE_URL } from '../config/api.config';

@Injectable({
  providedIn: 'root',
})
export class Signalr {
  private hubConnection: signalR.HubConnection | null = null;

  messageData = signal<string>('');

  constructor() {
    this.initConnection();
  }

  public sendMessage(msg: string){
    this.hubConnection?.invoke('SendSimulationData', msg)
      .catch((err) => {
        console.error('SignalR send message error: ', err);
      });
  }

  private initConnection(){
    this.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/simulation`, {
      accessTokenFactory: () => getStoredAccessToken() ?? '',
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .build();

    // 监听服务器发送的消息
    this.hubConnection.on('ReceiveSimulationData', (message: string) => {
      this.messageData.set(message);
    });

    // 启动连接
    this.hubConnection.start()
      .then(() => {
        console.log('SignalR connected.');
      })
      .catch((err) => {
        console.error('SignalR connection error: ', err);
      });
  }
}
