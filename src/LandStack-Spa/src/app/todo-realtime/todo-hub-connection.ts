import { InjectionToken } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { environment } from '../../environments/environment';

export type TodoHubConnection = Pick<HubConnection,
  'on' | 'onclose' | 'onreconnecting' | 'onreconnected' |
  'start' | 'stop' | 'invoke' | 'send'>;

export const TODO_HUB_CONNECTION_FACTORY = new InjectionToken<() => TodoHubConnection>(
  'TODO_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => () => new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
          if (retryContext.previousRetryCount < 6) return 10000;
          if (retryContext.previousRetryCount < 10) return 30000;
          return 60000;
        },
      })
      .build(),
  }
);
