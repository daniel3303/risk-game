import React from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource-variable/inter';
import '@fontsource-variable/roboto-condensed';
import './styles.css';
import './campaign.css';
import { App } from './ui/App';

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);
