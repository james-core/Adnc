﻿global using Adnc.Application.Contracts.Shared.Attributes;
global using Adnc.Application.Contracts.Shared.Services;
global using Adnc.Application.Shared.BloomFilter;
global using Adnc.Application.Shared.Caching;
global using Adnc.Application.Shared.Channels;
global using Adnc.Application.Shared.IdGenerater;
global using Adnc.Application.Shared.Interceptors;
global using Adnc.Infra.Caching;
global using Adnc.Infra.Caching.Core;
global using Adnc.Infra.Caching.Core.Diagnostics;
global using Adnc.Infra.Caching.Interceptor.Castle;
global using Adnc.Infra.Core;
global using Adnc.Infra.EfCore;
global using Adnc.Infra.Entities;
global using Adnc.Infra.Helper;
global using Adnc.Infra.Helper.IdGeneraterInternal;
global using Adnc.Infra.IRepositories;
global using Adnc.Infra.Mapper;
global using Adnc.Infra.Mapper.AutoMapper;
global using Adnc.Infra.Mongo;
global using Adnc.Infra.Mq;
global using Adnc.Shared.Consts.Caching.Common;
global using Adnc.Shared.ResultModels;
global using Autofac;
global using Autofac.Extras.DynamicProxy;
global using Castle.DynamicProxy;
global using FluentValidation;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Polly;
global using SkyApm;
global using SkyApm.Common;
global using SkyApm.Config;
global using SkyApm.Diagnostics;
global using SkyApm.Tracing;
global using SkyApm.Tracing.Segments;
global using SkyApm.Utilities.DependencyInjection;
global using System;
global using System.Collections.Generic;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Linq.Expressions;
global using System.Net;
global using System.Reflection;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;