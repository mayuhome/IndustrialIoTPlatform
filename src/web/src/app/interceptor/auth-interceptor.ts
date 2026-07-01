import { HttpInterceptorFn } from '@angular/common/http';
import { API_BASE_URL } from '../core/config/api.config';
import { getStoredAccessToken } from '../core/auth/auth-session';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const isAbsoluteUrl = /^https?:\/\//i.test(req.url);
  const url = !isAbsoluteUrl && req.url.startsWith('/')
    ? `${API_BASE_URL}${req.url}`
    : req.url;
  const token = getStoredAccessToken();

  const shouldAttachToken = token && !req.url.includes('/auth/login') && !req.url.includes('/auth/register');

  if (!shouldAttachToken && url === req.url) {
    return next(req);
  }

  const headers = shouldAttachToken
    ? {
        Authorization: `Bearer ${token}`
      }
    : undefined;

  return next(req.clone({ url, setHeaders: headers }));
};
